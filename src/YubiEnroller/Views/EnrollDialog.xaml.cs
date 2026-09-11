using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

        Loaded += (s, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                PinBox.Focus();
                Keyboard.Focus(PinBox);
            }, System.Windows.Threading.DispatcherPriority.Input);
        };

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

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EnrollViewModel.IsComplete) && _viewModel.IsComplete)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    SubmitButton.IsEnabled = false;
                    SubmitButton.IsDefault = false;
                    CloseButton.IsDefault = true;
                    CloseButton.Focus();
                    Keyboard.Focus(CloseButton);
                }, System.Windows.Threading.DispatcherPriority.Input);
            }
        };
    }

    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Pin = PinBox.Password;
    }

    private void PinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_viewModel.IsComplete)
            {
                Close();
                return;
            }

            if (_viewModel.EnrollCommand.CanExecute(null))
            {
                _viewModel.EnrollCommand.Execute(null);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
