using System;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Input;
using YubiEnroller.Models;
using YubiEnroller.Services;

namespace YubiEnroller.ViewModels;

public class EnrollViewModel : ViewModelBase
{
    private readonly IYubiKeyService _yubiService;
    private readonly WindowsCaEnrollmentService _caService;
    private readonly AppSettings _settings;

    private string _subjectCommonName = Environment.UserName;
    private string _userPrincipalName = $"{Environment.UserName}@{Environment.UserDomainName.ToLowerInvariant()}.local";
    private string _selectedTemplate = "SmartcardLogon";
    private string _caConfigString = string.Empty;
    private string _keyAlgorithm = "RSA2048";
    private string _pin = "123456";
    private bool _isEnrolling = false;
    private int _stepIndex = 0;
    private string _statusMessage = "Ready to enroll.";
    private string? _errorMessage;
    private bool _hasError = false;
    private bool _isComplete = false;
    private CertificateModel? _enrolledCertificate;

    public ObservableCollection<string> AvailableTemplates { get; } = new()
    {
        "SmartcardLogon",
        "SmartcardUser",
        "User",
        "ClientAuth"
    };

    public ObservableCollection<string> AvailableAlgorithms { get; } = new()
    {
        "RSA2048",
        "ECCP256"
    };

    public string SubjectCommonName
    {
        get => _subjectCommonName;
        set => SetProperty(ref _subjectCommonName, value);
    }

    public string UserPrincipalName
    {
        get => _userPrincipalName;
        set => SetProperty(ref _userPrincipalName, value);
    }

    public string SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public string CaConfigString
    {
        get => _caConfigString;
        set => SetProperty(ref _caConfigString, value);
    }

    public string KeyAlgorithm
    {
        get => _keyAlgorithm;
        set => SetProperty(ref _keyAlgorithm, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetProperty(ref _pin, value);
    }

    public bool IsEnrolling
    {
        get => _isEnrolling;
        set => SetProperty(ref _isEnrolling, value);
    }

    public int StepIndex
    {
        get => _stepIndex;
        set => SetProperty(ref _stepIndex, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                HasError = !string.IsNullOrEmpty(value);
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public bool IsComplete
    {
        get => _isComplete;
        set => SetProperty(ref _isComplete, value);
    }

    public CertificateModel? EnrolledCertificate
    {
        get => _enrolledCertificate;
        set => SetProperty(ref _enrolledCertificate, value);
    }

    public ICommand EnrollCommand { get; }

    public EnrollViewModel(
        IYubiKeyService yubiService,
        WindowsCaEnrollmentService caService,
        AppSettings settings)
    {
        _yubiService = yubiService;
        _caService = caService;
        _settings = settings;

        _caConfigString = settings.CaConfigString;
        _selectedTemplate = settings.CertificateTemplate;
        _keyAlgorithm = settings.DefaultKeyAlgorithm;

        EnrollCommand = new RelayCommand(async () => await StartEnrollmentAsync(), () => !IsEnrolling);
    }

    public async Task StartEnrollmentAsync()
    {
        if (string.IsNullOrWhiteSpace(Pin))
        {
            ErrorMessage = "Please enter your YubiKey PIN.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SubjectCommonName))
        {
            ErrorMessage = "Common Name is required.";
            return;
        }

        IsEnrolling = true;
        HasError = false;
        ErrorMessage = null;
        IsComplete = false;

        try
        {
            // Step 1: Generate Key and CSR on Token
            StepIndex = 1;
            StatusMessage = "Connecting to YubiKey PIV and generating asymmetric key pair on token...";
            await Task.Delay(300);

            string subjectDn = $"CN={SubjectCommonName.Trim()}";
            string csrPem = await _yubiService.GenerateCsrAsync(
                0x9A,
                subjectDn,
                string.IsNullOrWhiteSpace(UserPrincipalName) ? null : UserPrincipalName.Trim(),
                KeyAlgorithm,
                Pin);

            // Step 2: Submit to Windows CA
            StepIndex = 2;
            StatusMessage = $"Submitting CSR to Windows CA with template '{SelectedTemplate}'...";
            await Task.Delay(300);

            var caResult = await _caService.SubmitCsrAsync(
                csrPem,
                SelectedTemplate,
                CaConfigString,
                _yubiService.IsSimulator);

            if (!caResult.Success)
            {
                if (caResult.IsPendingApproval)
                {
                    StatusMessage = caResult.Message;
                    ErrorMessage = caResult.Message;
                }
                else
                {
                    ErrorMessage = caResult.Message;
                }
                IsEnrolling = false;
                return;
            }

            // Step 3: Install Certificate on Token
            StepIndex = 3;
            StatusMessage = "Writing issued X.509 certificate to YubiKey Slot 9a...";
            await Task.Delay(300);

            bool installed = await _yubiService.InstallCertificateAsync(
                0x9A,
                caResult.CertificateBytes!,
                Pin);

            if (!installed)
            {
                ErrorMessage = "Failed to import certificate into YubiKey.";
                IsEnrolling = false;
                return;
            }

            // Step 4: Verification
            StepIndex = 4;
            EnrolledCertificate = _yubiService.GetEnrolledCertificate(0x9A);
            StatusMessage = "PIV certificate successfully enrolled and installed!";
            IsComplete = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsEnrolling = false;
        }
    }
}
