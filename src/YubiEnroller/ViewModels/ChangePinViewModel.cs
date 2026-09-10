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

        if (string.IsNullOrWhiteSpace(CurrentPin))
        {
            ErrorMessage = "Please enter your current PIN.";
            RequestShowMessage?.Invoke("Missing Current PIN", ErrorMessage, true);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPin) || NewPin.Length < 6 || NewPin.Length > 8)
        {
            ErrorMessage = "New PIN must be between 6 and 8 characters.";
            RequestShowMessage?.Invoke("Invalid PIN Length", ErrorMessage, true);
            return;
        }

        if (NewPin != ConfirmNewPin)
        {
            ErrorMessage = "New PIN and confirmation do not match.";
            RequestShowMessage?.Invoke("PIN Mismatch", ErrorMessage, true);
            return;
        }

        IsBusy = true;

        try
        {
            var (success, retries, error) = await _yubiService.ChangePinAsync(CurrentPin, NewPin);

            if (retries.HasValue)
            {
                RetriesRemaining = retries.Value;
            }

            if (success)
            {
                IsSuccess = true;
                SuccessMessage = "PIN successfully changed! You can now use your new PIN.";
                RequestShowMessage?.Invoke("PIN Changed Successfully", "Your YubiKey PIV PIN has been successfully updated!", false);
                RequestClose?.Invoke();
            }
            else
            {
                ErrorMessage = error ?? "Failed to change PIN.";
                RequestShowMessage?.Invoke("PIN Change Failed", ErrorMessage, true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            RequestShowMessage?.Invoke("Error", ex.Message, true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
