using System;
using System.Threading.Tasks;
using YubiEnroller.Models;

namespace YubiEnroller.Services;

public interface IYubiKeyService : IDisposable
{
    event EventHandler<DeviceTelemetry?>? DeviceStateChanged;
    event EventHandler? CertificateChanged;

    DeviceTelemetry? CurrentDevice { get; }
    bool IsConnected { get; }
    bool IsSimulator { get; }

    CertificateModel? GetEnrolledCertificate(byte slot = 0x9A);

    Task<string> GenerateCsrAsync(
        byte slot,
        string subjectDn,
        string? upn,
        string keyType,
        string pin);

    Task<bool> InstallCertificateAsync(byte slot, byte[] certRawData, string pin);

    Task<(bool Success, int? RetriesRemaining, string? ErrorMessage)> ChangePinAsync(
        string currentPin,
        string newPin);

    Task<bool> DeleteCertificateAsync(byte slot, string pin);

    int GetPinRetries();

    void Refresh();
}
