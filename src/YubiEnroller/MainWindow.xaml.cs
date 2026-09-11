using System;
using System.Windows;
using Microsoft.Win32;
using YubiEnroller.Models;
using YubiEnroller.Services;
using YubiEnroller.ViewModels;
using YubiEnroller.Views;

namespace YubiEnroller;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(AppSettings? initialSettings)
    {
        InitializeComponent();

        var settings = initialSettings ?? AppSettings.Load();
        LocalizationService.Instance.SetLanguage(settings.Language);

        var hardwareService = new YubiKeyHardwareService();
        var simulatorService = new YubiKeySimulatorService();
        var caService = new WindowsCaEnrollmentService();

        _viewModel = new MainViewModel(hardwareService, simulatorService, caService, settings);
        DataContext = _viewModel;

        _viewModel.RequestEnrollDialog += OnRequestEnrollDialog;
        _viewModel.RequestPinDialog += OnRequestPinDialog;
        _viewModel.RequestDetailsDialog += OnRequestDetailsDialog;
        _viewModel.RequestSettingsDialog += OnRequestSettingsDialog;
        _viewModel.RequestShowMessage += (title, msg) => MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
        _viewModel.RequestSaveFilePath += OnRequestSaveFilePath;
    }

    private void OnRequestEnrollDialog()
    {
        var settings = AppSettings.Load();
        var enrollVm = new EnrollViewModel(
            _viewModel.GetActiveService(),
            _viewModel.GetCaService(),
            settings);

        var dialog = new EnrollDialog(enrollVm)
        {
            Owner = this
        };
        dialog.ShowDialog();

        _viewModel.Refresh();
    }

    private void OnRequestPinDialog()
    {
        var pinVm = new ChangePinViewModel(_viewModel.GetActiveService());
        var dialog = new ChangePinDialog(pinVm)
        {
            Owner = this
        };
        dialog.ShowDialog();

        _viewModel.Refresh();
    }

    private void OnRequestDetailsDialog()
    {
        if (_viewModel.EnrolledCertificate == null) return;

        var dialog = new CertDetailsDialog(_viewModel.EnrolledCertificate)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnRequestSettingsDialog()
    {
        var settings = AppSettings.Load();
        var dialog = new SettingsDialog(settings)
        {
            Owner = this
        };
        dialog.ShowDialog();

        if (dialog.SettingsSaved)
        {
            _viewModel.UpdateSettings(settings);
            LocalizationService.Instance.SetLanguage(settings.Language);
        }
        else
        {
            LocalizationService.Instance.SetLanguage(_viewModel.GetSettings().Language);
        }
    }

    private string? OnRequestSaveFilePath(string defaultName)
    {
        var sfd = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = "Certificate (*.cer)|*.cer|PEM Certificate (*.pem)|*.pem|All Files (*.*)|*.*",
            Title = "Export PIV Certificate"
        };
        return sfd.ShowDialog(this) == true ? sfd.FileName : null;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (_viewModel.GetActiveService() as IDisposable)?.Dispose();
    }
}