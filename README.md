# YubiEnroller

<p align="center">
  <img src="docs/screenshots/screenshot_enrolled_state.png?raw=true&v=2" alt="YubiEnroller Main Window" width="750" />
</p>

<p align="center">
  <strong>Modern, standalone Windows desktop application for YubiKey PIV credential management and Windows Active Directory Certificate Services (AD CS) enrollment.</strong>
</p>

<p align="center">
  <a href="https://github.com/npuee/yubi-enroller"><img src="https://img.shields.io/badge/GitHub-Repository-blue?logo=github" alt="GitHub Repo" /></a>
  <img src="https://img.shields.io/badge/.NET-8.0_WPF-512BD4?logo=dotnet" alt=".NET 8 WPF" />
  <img src="https://img.shields.io/badge/Platform-Windows_10_%2F_11_x64-0078D6?logo=windows" alt="Platform" />
  <img src="https://img.shields.io/badge/SDK-Yubico.YubiKey_1.13.0-54B948" alt="Yubico SDK" />
  <img src="https://img.shields.io/badge/Deployment-Single_File_Executable-success" alt="Deployment" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License" />
</p>

---

## Overview

**YubiEnroller** enables enterprise organizations to streamline the deployment of **YubiKey Smart Card Logon** credentials to end users without requiring complex scripts, third-party middleware, or external runtimes.

Built with **C# / .NET 8 (WPF)** and the official **Yubico.YubiKey SDK**, YubiEnroller compiles down to a **zero-dependency, standalone native Windows executable (`YubiEnroller.exe`)**. It runs out-of-the-box on standard corporate workstations without needing administrative software installers or pre-installed .NET runtimes.

---

## Screenshots

### 1. Active Certificate Enrolled (Slot 9a)
Displays real-time hardware telemetry, enrolled user identity, certificate expiration status pill, and quick actions.

<p align="center">
  <img src="docs/screenshots/screenshot_enrolled_state.png?raw=true&v=2" alt="Active Certificate State" width="720" />
</p>

---

### 2. Ready to Enroll / Empty State
When a YubiKey is inserted with no authentication certificate in Slot 9a, users are greeted with a clear call-to-action to enroll or update their PIN.

<p align="center">
  <img src="docs/screenshots/screenshot_empty_state.png?raw=true&v=2" alt="Empty State" width="720" />
</p>

---

### 3. Streamlined CA Enrollment Dialog
Designed for non-technical users. Automatically detects active Windows identity, loads enterprise certificate templates from `settings.json`, and hides cryptographic complexity.

<p align="center">
  <img src="docs/screenshots/screenshot_enroll_dialog.png?raw=true&v=2" alt="Enroll Dialog" width="480" />
</p>

---

### 4. PIN Management Dialog
Securely change YubiKey PIV PIN with attempt counter protection against lockouts, validation feedback, and clear success confirmation.

<p align="center">
  <img src="docs/screenshots/screenshot_change_pin.png?raw=true&v=2" alt="Change PIN Dialog" width="480" />
</p>

---

### 5. Settings & Diagnostics Dialog
Configure language, Windows CA server string, and open log or configuration files. Simulator mode is safely tucked away under an advanced diagnostics section.

<p align="center">
  <img src="docs/screenshots/screenshot_settings_dialog.png?raw=true&v=2" alt="Settings Dialog" width="480" />
</p>

---

### 6. No Device Connected State
Real-time USB insertion/removal monitoring informs the user when a token is disconnected.

<p align="center">
  <img src="docs/screenshots/screenshot_no_device.png?raw=true&v=2" alt="No Device State" width="720" />
</p>

---

## Key Features

- **Hardware PIV Slot 9a (Authentication) Inspection**:
  - Automatically queries and displays active X.509 certificate metadata:
    - Subject Common Name (CN) and User Principal Name (UPN)
    - Issuing Certificate Authority (CA)
    - Validity Range with dynamic status indicator (e.g. `● Valid (354d remaining)`)
    - Key algorithm and bit length (e.g. RSA 2048-bit on-chip)
    - SHA-256 Thumbprint and Serial Number
  - Export public certificate directly as `.cer` (DER format).
  - View full cryptographic certificate details modal.

- **Silent Headless CLI Provisioning (`--silent`)**:
  - Zero-touch enterprise automation for Intune, SCCM, and batch PowerShell provisioning scripts.
  - Generates on-chip keypairs, signs CSRs, submits to Active Directory CA, and imports certificates without launching a window.
  - Emits standard machine-readable process exit codes (`0 = Success`, `1 = Error`, `2 = Pending Approval`).

- **Enroll on Behalf Of (EOBO)**:
  - **GUI Enrollment Agent Mode**: Designed for Helpdesk and Security Officers. When enabled in Settings, allows selecting a target user (`DOMAIN\username` or UPN) to issue and provision credentials on physical tokens before handing them to employees.
  - **CLI Support (`--on-behalf-of <USER>`)**: Headless scripting allows centralized IT to automate bulk key provisioning for new hires.

- **Hardware Touch Sensor Prompting**:
  - Directly intercepts hardware touch sensor events via the Yubico .NET SDK `KeyCollector` (`KeyEntryRequest.TouchRequest`).
  - **GUI Visual Banner**: Displays a distinct amber-gold pulsing notice (`👆 Touch your YubiKey now to authorize...`) guiding users to touch their hardware token.
  - **Console Indicator**: Interactively alerts CLI operators: `[ACTION REQUIRED] >>> PLEASE TOUCH YOUR YUBIKEY SENSOR NOW <<<`.

- **Enterprise Windows CA Certificate Enrollment**:
  - **On-Token Key Generation**: Private keys are generated directly on the YubiKey PIV secure element and never leave the hardware token.
  - **Standard PKCS#10 CSR**: Constructs an industry-standard CSR with UPN Subject Alternative Name (SAN) and Smart Card Logon Enhanced Key Usages (EKU).
  - **Active Directory CA Integration**: Submits CSRs via `certreq.exe` to enterprise Windows CAs and writes the issued certificate directly into PIV Slot 9a.
  - **Factory Default PIN (`123456`) Guard**: If a user attempts to enroll with the factory default PIN, enrollment is blocked and the user is prompted to set a personal PIN first.

- **PIN Lifecycle Management**:
  - Update user PIV PIN with real-time retry count tracking (prevents accidental card lockout).
  - Enforces length requirements (6–8 digits/characters) and matching confirmation.
  - Immediate visual feedback on success or failure.

- **Status Bar Telemetry**:
  - Real-time hardware information: **Model** (`YubiKey 5 NFC`), **Serial Number** (`SN: 19482012`), **Firmware Version** (`FW: 5.4.3`), and **Remaining PIN Retries**.

- **Certificate Expiration Alerts & SCCM Compliance**:
  - **Custom Warning Threshold**: Configurable days-to-expiration threshold (`NotificationDaysBeforeExpiry`, default: 30 days) in `settings.json` and the GUI Settings dialog.
  - **Taskbar Alert Notification**: A non-intrusive bottom-right toast notification card alerting the user when a certificate is expiring soon or expired, offering a direct **"Renew Certificate Now"** button (launches re-enrollment) and **"Remind Me Later"**.
  - **SCCM / Scheduled Task Automation**: Run `YubiEnroller.exe --check-expiry` via Scheduled Task in the interactive user session, or `--check-expiry --silent` for headless compliance reporting (returns exit code `10` if expiring/expired, `0` if healthy or no token connected).

- **Multi-Language GUI Localization**:
  - Dynamic on-the-fly language switching (no restart required) across all views and dialogs.
  - Ships with 6 languages:
    - 🇬🇧 **English** (`en`)
    - 🇪🇪 **Estonian / Eesti** (`et`)
    - 🇩🇪 **German / Deutsch** (`de`)
    - 🇫🇷 **French / Français** (`fr`)
    - 🇪🇸 **Spanish / Español** (`es`)
    - 🇱🇹 **Lithuanian / Lietuvių** (`lt`)
  - Easily extensible: simply drop any new `<code >.json` into the `locales/` directory.

- **External Configuration (`settings.json`)**:
  - Shipped directly alongside `YubiEnroller.exe`.
  - Hot-reloaded whenever dialogs open, allowing administrators to modify configurations without restarting the app.

- **Diagnostics Logging (Off by Default)**:
  - High-performance, thread-safe logger writing to `yubi-enroller.log`.
  - **Disabled by default** to keep production environments clean and eliminate disk I/O.
  - **Can only be toggled via `settings.json`** (`"EnableLogging": true`), preventing standard users from changing logging behavior in the UI.

- **Built-in Hardware Simulator**:
  - Allows full demonstration, testing, and UI preview without requiring physical YubiKey hardware or an Active Directory domain controller.
  - Safely located under **Settings > Advanced / Diagnostics**.

---

## Silent Headless CLI Provisioning

YubiEnroller includes a built-in headless CLI engine for enterprise deployment automation, scheduled tasks, and bulk token provisioning:

```powershell
# Display help and CLI usage options:
.\YubiEnroller.exe --help

# Standard headless enrollment for logged-in workstation user:
.\YubiEnroller.exe --silent --pin 123456

# Helpdesk: Enroll on Behalf Of another user with a specific template and set a new personal PIN:
.\YubiEnroller.exe --silent --on-behalf-of CORP\jdoe --template SmartcardUser --pin 123456 --new-pin 829104

# Enforce hardware touch sensor requirement during enrollment:
.\YubiEnroller.exe --silent --pin 123456 --touch-policy Always

# Silent enrollment targeting custom CA template and specific CA server:
.\YubiEnroller.exe --silent --template SmartcardUser --ca "ca01.corp.local\Enterprise-CA" --pin 123456

# Check certificate expiration and display user notification popup if within threshold (or default 30 days):
.\YubiEnroller.exe --check-expiry

# Check certificate expiration with a custom 14-day threshold:
.\YubiEnroller.exe --check-expiry --days 14

# Headless SCCM compliance check (no UI; exits 10 if expiring/expired, 0 if healthy):
.\YubiEnroller.exe --check-expiry --silent
```

### CLI Command Options

| Argument | Shorthand | Description |
| :--- | :--- | :--- |
| `--check-expiry` | `--notify-expiry` | Inspect token certificate expiration and show interactive alert if expiring soon or expired. |
| `--days <DAYS>` | `-d` | Custom warning threshold in days for expiration check (overrides `settings.json`). |
| `--silent` | `-s` | Run in headless mode without showing any GUI windows. |
| `--on-behalf-of <USER>` | `-u` | Target user account for Enroll on Behalf Of (e.g. `DOMAIN\username` or `user@domain.com`). |
| `--pin <PIN>` | `-p` | Current/factory PIV PIN (required for headless enrollment). |
| `--new-pin <PIN>` | | Update the PIN to a new value during provisioning. |
| `--template <NAME>` | `-t` | Name of the Active Directory certificate template. |
| `--ca <CONFIG>` | | Active Directory CA config string (default: auto-discovery). |
| `--touch-policy <POLICY>` | | Hardware touch policy: `Default`, `Always`, `Cached`, `Never`. |
| `--slot <HEX>` | | Target PIV slot (default: `9A` for Authentication). |
| `--simulator` | | Force Virtual Simulator mode for offline testing. |
| `--help` | `-h` | Display the CLI reference manual. |

### Process Exit Codes

| Exit Code | Status | Meaning |
| :---: | :--- | :--- |
| **`0`** | **Success / Healthy** | Enrollment succeeded, or certificate is healthy / no token connected during expiry check. |
| **`1`** | **Error** | Invalid parameter, incorrect PIN, missing hardware, or enrollment rejection. |
| **`2`** | **Pending** | CA requires Certificate Officer approval (request taken under submission). |
| **`10`** | **Expiring / Expired** | Certificate is within warning threshold or expired (when running with `--check-expiry --silent`). |

---

## SCCM / Scheduled Task Automated Expiration Alerts

To notify end users before their YubiKey smart card certificates expire, create a Scheduled Task (or deploy one via SCCM / Microsoft Intune / Group Policy):

### Interactive User Alert Task
Runs inside the user's interactive logon session (e.g. daily at logon or at 10:00 AM):
```powershell
# Program / script:
C:\Program Files\YubiEnroller\YubiEnroller.exe

# Arguments:
--check-expiry
```
- **Behavior**:
  - If no YubiKey is plugged in: exits cleanly with code `0` (silent, no annoying errors).
  - If certificate has more than `NotificationDaysBeforeExpiry` days remaining: exits cleanly with code `0`.
  - If certificate is expiring soon or expired: displays an alert card in the bottom-right corner with token details, days remaining, and an immediate **"Renew Certificate Now"** button that opens YubiEnroller for one-click re-enrollment.

### SCCM Compliance Rule / Detection Script
To detect non-compliant machines headlessly without showing any UI:
```powershell
$proc = Start-Process -FilePath "C:\Program Files\YubiEnroller\YubiEnroller.exe" `
                      -ArgumentList "--check-expiry --silent" `
                      -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -eq 10) {
    Write-Output "Non-Compliant: Certificate expiring soon or expired"
    exit 1
}
Write-Output "Compliant"
exit 0
```

---

## Configuration (`settings.json`)

The application settings file resides right next to `YubiEnroller.exe`:

```json
{
  "Language": "en",
  "CaConfigString": "",
  "CertificateTemplate": "SmartcardLogon",
  "CertificateTemplates": [
    "SmartcardLogon",
    "SmartcardUser",
    "User",
    "ClientAuth"
  ],
  "NotificationDaysBeforeExpiry": 30,
  "DefaultSlot": 154,
  "SimulatorMode": false,
  "EnrollmentAgentMode": false,
  "DefaultTouchPolicy": "Default",
  "DefaultKeyAlgorithm": "RSA2048",
  "EnableLogging": false
}
```

### Configuration Options

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Language` | string | `"en"` | GUI language code (`"en"`, `"et"`, `"de"`, `"fr"`, `"es"`, `"lt"`). |
| `NotificationDaysBeforeExpiry` | number | `30` | Days before expiration to display warning status in UI and trigger `--check-expiry` alert popups. |
| `CaConfigString` | string | `""` | Windows CA config string (`"CA-Server.domain.local\CA-Name"`). Leave blank for auto-discovery. |
| `CertificateTemplates` | array | `[...]` | List of enterprise CA templates. The **first item** is automatically the default selection in the enrollment dialog. |
| `CertificateTemplate` | string | `"SmartcardLogon"` | Explicit default template name (optional; overrides first array element if present). |
| `DefaultSlot` | number | `154` | Target PIV slot (`154` = `0x9A` Authentication / Smart Card Logon). |
| `SimulatorMode` | boolean | `false` | Enables virtual token simulator for testing without physical tokens. |
| `EnrollmentAgentMode` | boolean | `false` | Enables GUI "Enroll on Behalf Of" fields for Helpdesk / Enrollment Agents. |
| `DefaultTouchPolicy` | string | `"Default"` | Hardware touch policy for key generation (`"Default"`, `"Always"`, `"Cached"`, `"Never"`). |
| `DefaultKeyAlgorithm` | string | `"RSA2048"` | Key generation algorithm (`"RSA2048"` or `"ECCP256"`). |
| `EnableLogging` | boolean | `false` | Enables diagnostic file logging to `yubi-enroller.log`. Toggleable only via this file. |

---

## Standalone Deployment

A ready-to-run release executable is located in the `publish` directory:

```
publish/
├── YubiEnroller.exe        (~154 MB standalone self-contained native binary)
├── settings.json           (External application settings)
└── locales/
    ├── en.json             (English)
    ├── et.json             (Estonian / Eesti)
    ├── de.json             (German / Deutsch)
    ├── fr.json             (French / Français)
    ├── es.json             (Spanish / Español)
    └── lt.json             (Lithuanian / Lietuvių)
```

### Running on Workstations
1. Copy the contents of `publish/` to any folder on a Windows 10 or 11 (x64) workstation.
2. Double-click `YubiEnroller.exe`.
3. No installers, admin rights or .NET dependencies are needed.

---

## Building from Source

### Prerequisites
- Windows 10 / 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or Visual Studio 2022+

### 1. Clone Repository
```powershell
git clone https://github.com/npuee/yubi-enroller.git
cd yubi-enroller
```

### 2. Build Solution
```powershell
dotnet build
```

### 3. Run Automated Tests
```powershell
dotnet test
```
*Runs all 22 unit, integration, CLI, and UI verification tests.*

### 4. Publish Single-File Executable
```powershell
dotnet publish src/YubiEnroller/YubiEnroller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/
```

---

## Project Architecture

```
yubi-enroller/
├── docs/
│   └── screenshots/               # GitHub documentation preview images
├── publish/                       # Standalone output directory
│   ├── YubiEnroller.exe           # Single-file native executable
│   ├── settings.json              # Runtime configuration file
│   └── locales/                   # Translation dictionaries (JSON)
├── src/
│   └── YubiEnroller/
│       ├── App.xaml / .cs         # Application entry point & dark theme brushes
│       ├── MainWindow.xaml / .cs  # Primary dashboard & certificate card
│       ├── locales/               # Source localization dictionaries
│       ├── Models/
│       │   ├── AppSettings.cs     # Configuration persistence & hot-reload
│       │   ├── CertificateModel.cs# X.509 metadata parser
│       │   └── DeviceTelemetry.cs # Serial, firmware, and retry counters
│       ├── Services/
│       │   ├── AppLogger.cs       # Thread-safe diagnostic file logger
│       │   ├── CliHandler.cs      # Headless CLI parser & automation engine
│       │   ├── IYubiKeyService.cs # Smart card hardware abstraction
│       │   ├── LocalizationService.cs # Dynamic string localization engine
│       │   ├── PivRsaSignatureGenerator.cs # Token-backed CSR signing
│       │   ├── WindowsCaEnrollmentService.cs # Active Directory certreq execution
│       │   ├── YubiKeyHardwareService.cs # Yubico.YubiKey SDK implementation
│       │   └── YubiKeySimulatorService.cs # In-memory demo simulator
│       ├── ViewModels/
│       │   ├── ChangePinViewModel.cs # PIN update logic & error handling
│       │   ├── EnrollViewModel.cs    # CSR generation & CA enrollment stepper
│       │   └── MainViewModel.cs      # Dashboard state & hardware events
│       └── Views/
│           ├── CertDetailsDialog.xaml # Full X.509 viewer modal
│           ├── ChangePinDialog.xaml   # PIN update modal
│           ├── EnrollDialog.xaml      # Streamlined CA enrollment modal
│           └── SettingsDialog.xaml    # Application preferences modal
└── tests/
    └── YubiEnroller.Tests/
        ├── CsrGenerationTests.cs      # RSA CSR verification tests
        ├── SimulatorAndEnrollmentTests.cs # Full end-to-end lifecycle & CLI tests
        └── UiCaptureTests.cs          # Screenshot capture tests
```

---

## Technologies & Libraries

- **Framework**: [.NET 8.0](https://dotnet.microsoft.com/) / Windows Presentation Foundation (WPF)
- **Hardware Integration**: [Yubico.YubiKey .NET SDK](https://github.com/Yubico/Yubico.NET.SDK) (Official CCID/PIV smart card driver)
- **Cryptography**: Standard `System.Security.Cryptography`, `System.Formats.Asn1`, and [BouncyCastle Cryptography](https://www.bouncycastle.org/csharp/) (for PKCS#10 CSR formatting)
- **Enrollment Backend**: Microsoft Windows Active Directory Certificate Services (`certreq.exe`)
- **Testing**: [xUnit](https://xunit.net/)

---

## License

This project is licensed under the [MIT License](LICENSE).
