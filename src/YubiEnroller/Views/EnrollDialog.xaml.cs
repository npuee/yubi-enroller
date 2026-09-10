using System.Threading.Tasks;
using System.Windows;
using YubiEnroller.Services;
using YubiEnroller.ViewModels;

namespace YubiEnroller.Views;

public partial class EnrollDialog : Window
{
    private readonly EnrollViewModel _viewModel;

    public EnrollDialog(EnrollViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        PinBox.Password = _viewModel.Pin;

        _viewModel.RequestDefaultPinChange += () =>
        {
            var result = MessageBox.Show(
                this,
                LocalizationService.Get("EnrollDialog_DefaultPinPrompt"),
                LocalizationService.Get("EnrollDialog_DefaultPinTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return Task.FromResult(result == MessageBoxResult.Yes);
        };

        _viewModel.RequestOpenChangePin += () =>
        {
            var pinVm = new ChangePinViewModel(_viewModel.YubiService);
            var pinDialog = new ChangePinDialog(pinVm)
            {
                Owner = this
            };
            pinDialog.ShowDialog();

            if (pinVm.IsSuccess)
            {
                PinBox.Password = string.Empty;
                _viewModel.Pin = string.Empty;
            }
        };
    }

    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Pin = PinBox.Password;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
