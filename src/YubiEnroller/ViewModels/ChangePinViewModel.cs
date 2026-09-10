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

    public ICommand ChangePinCommand { get; }

    public ChangePinViewModel(IYubiKeyService yubiService)
    {
        _yubiService = yubiService;
        _retriesRemaining = _yubiService.GetPinRetries();
        ChangePinCommand = new RelayCommand(async () => await ExecuteChangePinAsync(), () => !IsBusy && !IsSuccess);
    }

    public async Task ExecuteChangePinAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPin))
        {
            ErrorMessage = "Please enter your current PIN.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPin) || NewPin.Length < 6 || NewPin.Length > 8)
        {
            ErrorMessage = "New PIN must be between 6 and 8 characters.";
            return;
        }

        if (NewPin != ConfirmNewPin)
        {
            ErrorMessage = "New PIN and confirmation do not match.";
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
            }
            else
            {
                ErrorMessage = error ?? "Failed to change PIN.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
