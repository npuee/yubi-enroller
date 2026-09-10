using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using YubiEnroller.Models;
using Yubico.YubiKey;
using Yubico.YubiKey.Piv;
using SysPublicKey = System.Security.Cryptography.X509Certificates.PublicKey;

namespace YubiEnroller.Services;

public class YubiKeyHardwareService : IYubiKeyService
{
    private IYubiKeyDevice? _currentDevice;
    private readonly object _lock = new();

    public event EventHandler<DeviceTelemetry?>? DeviceStateChanged;
    public event EventHandler? CertificateChanged;

    public YubiKeyHardwareService()
    {
        try
        {
            YubiKeyDeviceListener.Instance.Arrived += OnDeviceArrived;
            YubiKeyDeviceListener.Instance.Removed += OnDeviceRemoved;
            ScanForDevices();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing YubiKeyDeviceListener: {ex.Message}");
        }
    }

    public DeviceTelemetry? CurrentDevice
    {
        get
        {
            lock (_lock)
            {
                if (_currentDevice == null) return null;
                return BuildTelemetry(_currentDevice);
            }
        }
    }

    public bool IsConnected => _currentDevice != null;
    public bool IsSimulator => false;

    private void ScanForDevices()
    {
        lock (_lock)
        {
            var devices = YubiKeyDevice.FindByTransport(Transport.All).ToList();
            var prevDevice = _currentDevice;
            _currentDevice = devices.FirstOrDefault();

            if (_currentDevice != prevDevice)
            {
                DeviceStateChanged?.Invoke(this, CurrentDevice);
                CertificateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnDeviceArrived(object? sender, YubiKeyDeviceEventArgs e)
    {
        lock (_lock)
        {
            _currentDevice = e.Device;
            DeviceStateChanged?.Invoke(this, CurrentDevice);
            CertificateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDeviceRemoved(object? sender, YubiKeyDeviceEventArgs e)
    {
        lock (_lock)
        {
            if (_currentDevice?.SerialNumber == e.Device.SerialNumber)
            {
                _currentDevice = null;
                DeviceStateChanged?.Invoke(this, null);
                CertificateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public CertificateModel? GetEnrolledCertificate(byte slot = 0x9A)
    {
        lock (_lock)
        {
            if (_currentDevice == null) return null;

            try
            {
                using var piv = new PivSession(_currentDevice);
                var cert = piv.GetCertificate(slot);
                if (cert != null)
                {
                    return CertificateModel.FromX509Certificate2(cert, slot);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCertificate slot {slot:X2} failed: {ex.Message}");
            }
            return null;
        }
    }

    public Task<string> GenerateCsrAsync(
        byte slot,
        string subjectDn,
        string? upn,
        string keyType,
        string pin)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_currentDevice == null)
                    throw new InvalidOperationException("No YubiKey connected.");

                using var piv = new PivSession(_currentDevice);

                // Verify PIN
                var pinBytes = Encoding.UTF8.GetBytes(pin);
                if (!piv.TryVerifyPin(pinBytes, out int? retries))
                {
                    throw new UnauthorizedAccessException($"Invalid PIN. {retries} retries remaining.");
                }

                // Generate on-token key pair
                var algo = keyType == "RSA2048" ? PivAlgorithm.Rsa2048 : PivAlgorithm.EccP256;
#pragma warning disable CS0618
                var pivPublicKey = piv.GenerateKeyPair(
                    slot,
                    algo,
                    PivPinPolicy.Default,
                    PivTouchPolicy.Default);
#pragma warning restore CS0618

                // Export public key and create .NET PublicKey
                byte[] spki = pivPublicKey.ExportSubjectPublicKeyInfo();
                var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(spki, out _);

                var subject = new X500DistinguishedName(subjectDn);
                var request = new CertificateRequest(
                    subject,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                if (!string.IsNullOrWhiteSpace(upn))
                {
                    var sanBuilder = new SubjectAlternativeNameBuilder();
                    sanBuilder.AddUserPrincipalName(upn);
                    request.CertificateExtensions.Add(sanBuilder.Build());
                }

                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

                var eku = new OidCollection
                {
                    new Oid("1.3.6.1.4.1.311.20.2.2", "Smart Card Logon"),
                    new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication")
                };
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));

                // Sign the CSR using the YubiKey on-board private key
                var signer = new PivRsaSignatureGenerator(
                    piv,
                    slot,
                    request.PublicKey,
                    2048);

                byte[] csrDer = request.CreateSigningRequest(signer);
                return PemEncoding.WriteString("CERTIFICATE REQUEST", csrDer);
            }
        });
    }

    public Task<bool> InstallCertificateAsync(byte slot, byte[] certRawData, string pin)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_currentDevice == null)
                    throw new InvalidOperationException("No YubiKey connected.");

                using var piv = new PivSession(_currentDevice);

                // Verify PIN
                var pinBytes = Encoding.UTF8.GetBytes(pin);
                piv.TryVerifyPin(pinBytes, out _);

                var cert = new X509Certificate2(certRawData);
                piv.ImportCertificate(slot, cert, compress: false);

                CertificateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        });
    }

    public Task<(bool Success, int? RetriesRemaining, string? ErrorMessage)> ChangePinAsync(
        string currentPin,
        string newPin)
    {
        return Task.Run<(bool Success, int? RetriesRemaining, string? ErrorMessage)>(() =>
        {
            lock (_lock)
            {
                if (_currentDevice == null)
                    return (false, 0, "No YubiKey connected.");

                try
                {
                    using var piv = new PivSession(_currentDevice);
                    var curBytes = Encoding.UTF8.GetBytes(currentPin);
                    var newBytes = Encoding.UTF8.GetBytes(newPin);

                    bool success = piv.TryChangePin(curBytes, newBytes, out int? retries);
                    DeviceStateChanged?.Invoke(this, CurrentDevice);

                    if (success)
                    {
                        return (true, retries, null);
                    }
                    else
                    {
                        return (false, retries, $"PIN change failed. {retries} retries remaining.");
                    }
                }
                catch (Exception ex)
                {
                    return (false, 0, ex.Message);
                }
            }
        });
    }

    public Task<bool> DeleteCertificateAsync(byte slot, string pin)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_currentDevice == null) return false;
                try
                {
                    using var piv = new PivSession(_currentDevice);
                    var pinBytes = Encoding.UTF8.GetBytes(pin);
                    piv.TryVerifyPin(pinBytes, out _);
                    piv.DeleteKey(slot);
                    CertificateChanged?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        });
    }

    public int GetPinRetries()
    {
        lock (_lock)
        {
            if (_currentDevice == null) return 0;
            try
            {
                using var piv = new PivSession(_currentDevice);
                var meta = piv.GetMetadata(PivSlot.Pin);
                return meta.RetryCount;
            }
            catch
            {
                return 3;
            }
        }
    }

    public void Refresh()
    {
        ScanForDevices();
    }

    private DeviceTelemetry BuildTelemetry(IYubiKeyDevice device)
    {
        int retries = 3;
        try
        {
            using var piv = new PivSession(device);
            var meta = piv.GetMetadata(PivSlot.Pin);
            retries = meta.RetryCount;
        }
        catch { }

        return new DeviceTelemetry
        {
            SerialNumber = device.SerialNumber,
            ModelName = device.FormFactor.ToString().Contains("Nfc") ? "YubiKey 5 NFC" : "YubiKey 5 Series",
            FirmwareVersion = device.FirmwareVersion?.ToString() ?? "5.4.3",
            FormFactor = device.FormFactor.ToString(),
            IsSimulator = false,
            PinRetriesRemaining = retries,
            IsPivAvailable = true
        };
    }

    public void Dispose()
    {
        try
        {
            YubiKeyDeviceListener.Instance.Arrived -= OnDeviceArrived;
            YubiKeyDeviceListener.Instance.Removed -= OnDeviceRemoved;
        }
        catch { }
    }
}
