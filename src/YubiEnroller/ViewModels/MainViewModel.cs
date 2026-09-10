using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using YubiEnroller.Models;
using YubiEnroller.Services;

namespace YubiEnroller.ViewModels;

public class MainViewModel : ViewModelBase
{
    private IYubiKeyService _activeService;
    private readonly YubiKeyHardwareService _hardwareService;
    private readonly YubiKeySimulatorService _simulatorService;
    private readonly WindowsCaEnrollmentService _caService;
    private readonly AppSettings _settings;

    private DeviceTelemetry? _currentDevice;
    private CertificateModel? _enrolledCertificate;
    private bool _isSimulatorMode;
    private bool _isBusy;
    private string _statusMessage = "Ready";

    public DeviceTelemetry? CurrentDevice
    {
        get => _currentDevice;
        private set
        {
            if (SetProperty(ref _currentDevice, value))
            {
                OnPropertyChanged(nameof(HasDevice));
                OnPropertyChanged(nameof(DeviceModel));
                OnPropertyChanged(nameof(DeviceSerial));
                OnPropertyChanged(nameof(DeviceFirmware));
                OnPropertyChanged(nameof(PinRetriesText));
                OnPropertyChanged(nameof(ConnectionBadgeText));
                OnPropertyChanged(nameof(IsConnected));
            }
        }
    }

    public CertificateModel? EnrolledCertificate
    {
        get => _enrolledCertificate;
        private set
        {
            if (SetProperty(ref _enrolledCertificate, value))
            {
                OnPropertyChanged(nameof(HasCertificate));
            }
        }
    }

    public bool HasDevice => CurrentDevice != null;
    public bool HasCertificate => EnrolledCertificate != null;
    public bool IsConnected => _activeService.IsConnected;

    public bool IsSimulatorMode
    {
        get => _isSimulatorMode;
        set
        {
            if (SetProperty(ref _isSimulatorMode, value))
            {
                OnPropertyChanged(nameof(ModeDisplayText));
                OnPropertyChanged(nameof(SimulatorToggleText));
                SwitchService(value);
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Telemetry properties for bottom taskbar
    public string DeviceModel => CurrentDevice?.DisplayModel ?? "No Device";
    public string DeviceSerial => CurrentDevice != null ? $"SN: {CurrentDevice.DisplaySerial}" : "SN: ---";
    public string DeviceFirmware => CurrentDevice != null ? $"FW: {CurrentDevice.DisplayFirmware}" : "FW: ---";
    public string PinRetriesText => CurrentDevice != null ? $"PIN Retries: {CurrentDevice.PinRetriesRemaining}" : "PIN: ---";
    public string CaStatusText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_settings.CaConfigString))
                return $"CA: {_settings.CaConfigString}";
            return "CA: Active Directory (Auto)";
        }
    }

    public string ModeDisplayText => IsSimulatorMode ? "Simulator" : "Physical";
    public string SimulatorToggleText => IsSimulatorMode ? "Switch to Physical" : "Switch to Simulator";

    public string ConnectionBadgeText
    {
        get
        {
            if (IsSimulatorMode) return "SIMULATOR MODE";
            if (HasDevice) return "YUBIKEY CONNECTED";
            return "NO DEVICE DETECTED";
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ToggleSimulatorCommand { get; }
    public ICommand EnrollCommand { get; }
    public ICommand ChangePinCommand { get; }
    public ICommand ViewDetailsCommand { get; }
    public ICommand ExportCertCommand { get; }
    public ICommand DeleteCertCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public event Action? RequestEnrollDialog;
    public event Action? RequestPinDialog;
    public event Action? RequestDetailsDialog;
    public event Action? RequestSettingsDialog;
    public event Action<string, string>? RequestShowMessage;
    public event Func<string, string?>? RequestSaveFilePath;

    public MainViewModel(
        YubiKeyHardwareService hardwareService,
        YubiKeySimulatorService simulatorService,
        WindowsCaEnrollmentService caService,
        AppSettings settings)
    {
        _hardwareService = hardwareService;
        _simulatorService = simulatorService;
        _caService = caService;
        _settings = settings;

        _isSimulatorMode = settings.SimulatorMode;
        _activeService = _isSimulatorMode ? _simulatorService : _hardwareService;
        HookServiceEvents(_activeService);

        RefreshCommand = new RelayCommand(Refresh);
        ToggleSimulatorCommand = new RelayCommand(() => IsSimulatorMode = !IsSimulatorMode);
        EnrollCommand = new RelayCommand(() => RequestEnrollDialog?.Invoke(), () => HasDevice);
        ChangePinCommand = new RelayCommand(() => RequestPinDialog?.Invoke(), () => HasDevice);
        ViewDetailsCommand = new RelayCommand(() => RequestDetailsDialog?.Invoke(), () => HasCertificate);
        ExportCertCommand = new RelayCommand(ExportCertificate, () => HasCertificate);
        DeleteCertCommand = new RelayCommand(async () => await DeleteCertificateAsync(), () => HasCertificate);
        OpenSettingsCommand = new RelayCommand(() => RequestSettingsDialog?.Invoke());

        Refresh();
    }

    private void SwitchService(bool useSimulator)
    {
        UnhookServiceEvents(_activeService);
        _activeService = useSimulator ? _simulatorService : _hardwareService;
        HookServiceEvents(_activeService);
        _settings.SimulatorMode = useSimulator;
        _settings.Save();
        Refresh();
    }

    private void HookServiceEvents(IYubiKeyService service)
    {
        service.DeviceStateChanged += OnDeviceStateChanged;
        service.CertificateChanged += OnCertificateChanged;
    }

    private void UnhookServiceEvents(IYubiKeyService service)
    {
        service.DeviceStateChanged -= OnDeviceStateChanged;
        service.CertificateChanged -= OnCertificateChanged;
    }

    private void OnDeviceStateChanged(object? sender, DeviceTelemetry? device)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            CurrentDevice = device;
            LoadCertificate();
        });
    }

    private void OnCertificateChanged(object? sender, EventArgs e)
    {
        App.Current?.Dispatcher.Invoke(LoadCertificate);
    }

    public void Refresh()
    {
        _activeService.Refresh();
        CurrentDevice = _activeService.CurrentDevice;
        LoadCertificate();
        OnPropertyChanged(nameof(CaStatusText));
    }

    public void LoadCertificate()
    {
        if (_activeService.IsConnected)
        {
            EnrolledCertificate = _activeService.GetEnrolledCertificate(0x9A);
        }
        else
        {
            EnrolledCertificate = null;
        }
    }

    private void ExportCertificate()
    {
        if (EnrolledCertificate == null) return;

        string defaultFileName = $"{EnrolledCertificate.CommonName}_Slot9a.cer";
        string? path = RequestSaveFilePath?.Invoke(defaultFileName);
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                File.WriteAllBytes(path, EnrolledCertificate.RawBytes);
                RequestShowMessage?.Invoke("Certificate Export", $"Successfully exported certificate to:\n{path}");
            }
            catch (Exception ex)
            {
                RequestShowMessage?.Invoke("Export Error", $"Failed to save certificate: {ex.Message}");
            }
        }
    }

    private async Task DeleteCertificateAsync()
    {
        if (EnrolledCertificate == null) return;
        try
        {
            IsBusy = true;
            bool ok = await _activeService.DeleteCertificateAsync(0x9A, "123456");
            if (ok)
            {
                LoadCertificate();
                RequestShowMessage?.Invoke("Certificate Removed", "Certificate was deleted from Slot 9a.");
            }
        }
        catch (Exception ex)
        {
            RequestShowMessage?.Invoke("Delete Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public IYubiKeyService GetActiveService() => _activeService;
    public WindowsCaEnrollmentService GetCaService() => _caService;
    public AppSettings GetSettings() => _settings;
}
