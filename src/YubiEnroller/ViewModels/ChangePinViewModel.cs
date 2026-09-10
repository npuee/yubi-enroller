using System;
using System.Threading.Tasks;
using System.Windows.Input;
using YubiEnroller.Services;

namespace YubiEnroller.ViewModels;

public class ChangePinViewModel : ViewModelBase
{
    private readonly IYubiKeyService _yubiService;

    private string _currentPin = string.Empty;
    private string _newPin = string.Empty;
    private string _confirmNewPin = string.Empty;
    private int _retriesRemaining = 3;
    private string? _errorMessage;
    private string? _successMessage;
    private bool _isBusy = false;
    private bool _isSuccess = false;

    public string CurrentPin
    {
        get => _currentPin;
        set => SetProperty(ref _currentPin, value);
    }

    public string NewPin
    {
        get => _newPin;
        set => SetProperty(ref _newPin, value);
    }

    public string ConfirmNewPin
    {
        get => _confirmNewPin;
        set => SetProperty(ref _confirmNewPin, value);
    }

    public int RetriesRemaining
    {
        get => _retriesRemaining;
        set => SetProperty(ref _retriesRemaining, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string? SuccessMessage
    {
        get => _successMessage;
        set => SetProperty(ref _successMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetProperty(ref _isSuccess, value);
    }

    public event Action? RequestClose;
    public event Action<string, string, bool>? RequestShowMessage;

    public ICommand ChangePinCommand { get; }

    public ChangePinViewModel(IYubiKeyService yubiService)
    {
        _yubiService = yubiService;
        _retriesRemaining = _yubiService.GetPinRetries();
        ChangePinCommand = new RelayCommand(async () => await ExecuteChangePinAsync(), () => !IsBusy);
    }

    public async Task ExecuteChangePinAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        AppLogger.Info("ChangePinViewModel: ExecuteChangePinAsync invoked.");

        if (string.IsNullOrWhiteSpace(CurrentPin))
        {
            ErrorMessage = LocalizationService.Get("PinDialog_MissingCurrent");
            AppLogger.Warn($"ChangePinViewModel: {ErrorMessage}");
            RequestShowMessage?.Invoke(LocalizationService.Get("PinDialog_MissingCurrentTitle"), ErrorMessage, true);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPin) || NewPin.Length < 6 || NewPin.Length > 8)
        {
            ErrorMessage = LocalizationService.Get("PinDialog_InvalidLength");
            AppLogger.Warn($"ChangePinViewModel: {ErrorMessage}");
            RequestShowMessage?.Invoke(LocalizationService.Get("PinDialog_InvalidLengthTitle"), ErrorMessage, true);
            return;
        }

        if (NewPin != ConfirmNewPin)
        {
            ErrorMessage = LocalizationService.Get("PinDialog_Mismatch");
            AppLogger.Warn($"ChangePinViewModel: {ErrorMessage}");
            RequestShowMessage?.Invoke(LocalizationService.Get("PinDialog_MismatchTitle"), ErrorMessage, true);
            return;
        }

        IsBusy = true;
        AppLogger.Info("ChangePinViewModel: Invoking _yubiService.ChangePinAsync...");

        try
        {
            var (success, retries, error) = await _yubiService.ChangePinAsync(CurrentPin, NewPin);
            AppLogger.Info($"ChangePinViewModel: ChangePinAsync returned success={success}, retries={retries}, error='{error}'");

            if (retries.HasValue)
            {
                RetriesRemaining = retries.Value;
            }

            if (success)
            {
                IsSuccess = true;
                SuccessMessage = LocalizationService.Get("PinDialog_SuccessMessage");
                AppLogger.Info("ChangePinViewModel: Successfully updated PIN.");
                RequestShowMessage?.Invoke(LocalizationService.Get("PinDialog_SuccessTitle"), SuccessMessage, false);
                RequestClose?.Invoke();
            }
            else
            {
                ErrorMessage = error ?? LocalizationService.Get("PinDialog_FailTitle");
                AppLogger.Warn($"ChangePinViewModel: PIN change failed: {ErrorMessage}");
                RequestShowMessage?.Invoke(LocalizationService.Get("PinDialog_FailTitle"), ErrorMessage, true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            AppLogger.Error("ChangePinViewModel: Exception during ExecuteChangePinAsync", ex);
            RequestShowMessage?.Invoke("Error", ex.Message, true);
        }
        finally
        {
            IsBusy = false;
            AppLogger.Info("ChangePinViewModel: Execution completed, IsBusy=false.");
        }
    }
}
