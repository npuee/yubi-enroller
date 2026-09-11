<#
.SYNOPSIS
    Fixes Windows Smart Card Logon & RDP authentication for non-domain-joined workstations.
.DESCRIPTION
    1. Downloads and installs NPU Root CA into Local Machine Root and Enterprise NTAuth stores.
    2. Downloads and caches the Base CRL locally to prevent offline revocation failures.
    3. Configures Windows Kerberos to allow cached CRLs / ignore unreachable Delta CRLs.
    4. Relaxes KDC validation for non-domain clients.
#>

# Requires Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] This script must be run as Administrator! Please re-open PowerShell as Administrator." -ForegroundColor Red
    Exit 1
}

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Fixing Smart Card RDP Authentication (Non-Domain) " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Download CA Certificate and Add to NTAuth & Root
$caUrl = "https://pki.npu.ee/NPU-DC-01.npu.house_NPU%20Root%20CA.crt"
$caCertFile = "$env:TEMP\NPU_Root_CA.crt"

Write-Host "`n[1/4] Downloading CA certificate..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $caUrl -OutFile $caCertFile -UseBasicParsing

Write-Host "[2/4] Installing CA certificate into Trusted Root and Enterprise NTAuth stores..." -ForegroundColor Yellow
certutil -addstore -f Root $caCertFile | Out-Null
certutil -enterprise -addstore -f NTAuth $caCertFile | Out-Null
Write-Host "      -> Installed in Root & NTAuth successfully." -ForegroundColor Green

# 2. Download and Cache Base CRL Locally
$crlUrl = "https://pki.npu.ee/NPU%20Root%20CA.crl"
$crlFile = "$env:TEMP\NPU_Root_CA.crl"

Write-Host "`n[3/4] Downloading Base CRL and caching in local CA store..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $crlUrl -OutFile $crlFile -UseBasicParsing
certutil -addstore -f CA $crlFile | Out-Null
Write-Host "      -> Base CRL cached successfully." -ForegroundColor Green

# 3. Configure Kerberos Registry Settings for Non-Domain Clients
Write-Host "`n[4/4] Configuring Kerberos parameters for non-domain smart card logon..." -ForegroundColor Yellow
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\Kerberos\Parameters"
if (-not (Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
}

# Ignore offline Delta CRL / redirect issues
Set-ItemProperty -Path $regPath -Name "UseCachedCRLOnlyAndIgnoreRevocationUnknownErrors" -Value 1 -Type DWord -Force

# Relax KDC certificate validation for non-domain machine
Set-ItemProperty -Path $regPath -Name "KdcValidation" -Value 0 -Type DWord -Force

Write-Host "      -> UseCachedCRLOnlyAndIgnoreRevocationUnknownErrors = 1" -ForegroundColor Green
Write-Host "      -> KdcValidation = 0" -ForegroundColor Green

Write-Host "`n====================================================" -ForegroundColor Green
Write-Host " ALL FIXES APPLIED SUCCESSFULLY!                    " -ForegroundColor Green
Write-Host " You can now connect via RDP (mstsc.exe).           " -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
