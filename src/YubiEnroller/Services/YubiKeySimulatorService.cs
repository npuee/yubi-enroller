using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using YubiEnroller.Models;

namespace YubiEnroller.Services;

public class YubiKeySimulatorService : IYubiKeyService
{
    private string _currentPin = "123456";
    private int _retriesRemaining = 3;
    private CertificateModel? _enrolledCertificate;
    private bool _isConnected = true;
    private RSA? _simulatedPrivateKey;

    public event EventHandler<DeviceTelemetry?>? DeviceStateChanged;
    public event EventHandler? CertificateChanged;
    public event EventHandler<bool>? TouchRequired;

    public DeviceTelemetry? CurrentDevice => _isConnected ? new DeviceTelemetry
    {
        SerialNumber = 19482012,
        ModelName = "YubiKey 5 NFC (Simulator)",
        FirmwareVersion = "5.4.3",
        FormFactor = "USB-A",
        IsSimulator = true,
        PinRetriesRemaining = _retriesRemaining,
        IsPivAvailable = true
    } : null;

    public bool IsConnected => _isConnected;
    public bool IsSimulator => true;

    public CertificateModel? GetEnrolledCertificate(byte slot = 0x9A)
    {
        if (!_isConnected) return null;
        return _enrolledCertificate;
    }

    public async Task<string> GenerateCsrAsync(
        byte slot,
        string subjectDn,
        string? upn,
        string keyType,
        string pin,
        string touchPolicy = "Default")
    {
        if (!_isConnected) throw new InvalidOperationException("No YubiKey connected.");
        if (pin != _currentPin)
        {
            _retriesRemaining = Math.Max(0, _retriesRemaining - 1);
            DeviceStateChanged?.Invoke(this, CurrentDevice);
            throw new UnauthorizedAccessException($"Invalid PIN. {_retriesRemaining} attempts remaining.");
        }

        _retriesRemaining = 3;

        // If touch policy is enabled, simulate a brief touch request
        if (touchPolicy.Equals("always", StringComparison.OrdinalIgnoreCase) ||
            touchPolicy.Equals("cached", StringComparison.OrdinalIgnoreCase))
        {
            TouchRequired?.Invoke(this, true);
            await Task.Delay(1000);
            TouchRequired?.Invoke(this, false);
        }

        // Generate simulated keypair
        _simulatedPrivateKey?.Dispose();
        _simulatedPrivateKey = RSA.Create(keyType == "RSA2048" ? 2048 : 2048);

        var subject = new X500DistinguishedName(subjectDn);
        var request = new CertificateRequest(
            subject,
            _simulatedPrivateKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        if (!string.IsNullOrWhiteSpace(upn))
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddUserPrincipalName(upn);
            request.CertificateExtensions.Add(sanBuilder.Build());
        }

        // Add Key Usage (Digital Signature, Key Encipherment)
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        // Add Enhanced Key Usage (Smart Card Logon, Client Authentication)
        var ekuCollection = new OidCollection
        {
            new Oid("1.3.6.1.4.1.311.20.2.2", "Smart Card Logon"),
            new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication")
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(ekuCollection, critical: false));

        byte[] csrDer = request.CreateSigningRequest();
        string csrPem = PemEncoding.WriteString("CERTIFICATE REQUEST", csrDer);
        return csrPem;
    }

    public Task<bool> InstallCertificateAsync(byte slot, byte[] certRawData, string pin)
    {
        if (!_isConnected) throw new InvalidOperationException("No YubiKey connected.");
        if (pin != _currentPin)
        {
            _retriesRemaining = Math.Max(0, _retriesRemaining - 1);
            DeviceStateChanged?.Invoke(this, CurrentDevice);
            throw new UnauthorizedAccessException($"Invalid PIN. {_retriesRemaining} attempts remaining.");
        }

        var x509 = new X509Certificate2(certRawData);
        _enrolledCertificate = CertificateModel.FromX509Certificate2(x509, slot);
        CertificateChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(true);
    }

    public Task<(bool Success, int? RetriesRemaining, string? ErrorMessage)> ChangePinAsync(
        string currentPin,
        string newPin)
    {
        if (!_isConnected)
            return Task.FromResult((false, (int?)0, (string?)"No YubiKey connected."));

        if (_retriesRemaining <= 0)
            return Task.FromResult((false, (int?)0, (string?)"PIN is blocked. Reset required."));

        if (currentPin != _currentPin)
        {
            _retriesRemaining = Math.Max(0, _retriesRemaining - 1);
            DeviceStateChanged?.Invoke(this, CurrentDevice);
            return Task.FromResult((false, (int?)_retriesRemaining, (string?)$"Incorrect PIN. {_retriesRemaining} retries remaining."));
        }

        if (string.IsNullOrWhiteSpace(newPin) || newPin.Length < 6 || newPin.Length > 8)
        {
            return Task.FromResult((false, (int?)_retriesRemaining, (string?)"New PIN must be between 6 and 8 characters."));
        }

        _currentPin = newPin;
        _retriesRemaining = 3;
        DeviceStateChanged?.Invoke(this, CurrentDevice);
        return Task.FromResult((true, (int?)3, (string?)null));
    }

    public Task<bool> DeleteCertificateAsync(byte slot, string pin)
    {
        if (!_isConnected) return Task.FromResult(false);
        if (pin != _currentPin)
        {
            _retriesRemaining = Math.Max(0, _retriesRemaining - 1);
            DeviceStateChanged?.Invoke(this, CurrentDevice);
            throw new UnauthorizedAccessException($"Invalid PIN. {_retriesRemaining} attempts remaining.");
        }

        _enrolledCertificate = null;
        CertificateChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(true);
    }

    public int GetPinRetries() => _retriesRemaining;

    public void Refresh()
    {
        DeviceStateChanged?.Invoke(this, CurrentDevice);
        CertificateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetConnected(bool connected)
    {
        _isConnected = connected;
        DeviceStateChanged?.Invoke(this, CurrentDevice);
        CertificateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SeedSampleCertificate()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=John Doe, OU=Security, DC=corp, DC=local",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUserPrincipalName("jdoe@corp.local");
        sanBuilder.AddEmailAddress("john.doe@corp.local");
        req.CertificateExtensions.Add(sanBuilder.Build());

        var eku = new OidCollection
        {
            new Oid("1.3.6.1.4.1.311.20.2.2", "Smart Card Logon"),
            new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication")
        };
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));

        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-10),
            DateTimeOffset.UtcNow.AddDays(355));

        _enrolledCertificate = CertificateModel.FromX509Certificate2(cert, 0x9A);
        _enrolledCertificate.Issuer = "CN=Corporate-Issuing-CA, DC=corp, DC=local";
        _enrolledCertificate.IssuerCommonName = "Corporate-Issuing-CA";
        CertificateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _simulatedPrivateKey?.Dispose();
    }
}
