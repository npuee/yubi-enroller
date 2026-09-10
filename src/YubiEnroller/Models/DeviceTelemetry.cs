namespace YubiEnroller.Models;

public class DeviceTelemetry
{
    public int? SerialNumber { get; set; }
    public string ModelName { get; set; } = "Unknown";
    public string FirmwareVersion { get; set; } = "Unknown";
    public string FormFactor { get; set; } = "USB-A";
    public bool IsSimulator { get; set; }
    public int PinRetriesRemaining { get; set; } = 3;
    public bool IsPivAvailable { get; set; } = true;

    public string DisplaySerial => SerialNumber.HasValue ? SerialNumber.Value.ToString() : "N/A";
    public string DisplayFirmware => string.IsNullOrWhiteSpace(FirmwareVersion) ? "Unknown" : FirmwareVersion;
    public string DisplayModel => string.IsNullOrWhiteSpace(ModelName) ? "YubiKey" : ModelName;

    public string StatusText => IsSimulator
        ? "Simulator Mode Active"
        : $"Connected ({FormFactor})";
}
