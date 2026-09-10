using System.Windows;
using System.Windows.Input;
using YubiEnroller.ViewModels;

namespace YubiEnroller.Views;

public partial class ChangePinDialog : Window
{
    private readonly ChangePinViewModel _viewModel;

    public ChangePinDialog(ChangePinViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += () =>
        {
            Dispatcher.Invoke(() =>
            {
                DialogResult = true;
                Close();
            });
        };

        _viewModel.RequestShowMessage += (title, msg, isError) =>
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    this,
                    msg,
                    title,
                    MessageBoxButton.OK,
                    isError ? MessageBoxImage.Warning : MessageBoxImage.Information);
            });
        };

        CurrentPinBox.Focus();
    }

    private void CurrentPinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentPin = CurrentPinBox.Password;
    }

    private void NewPinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.NewPin = NewPinBox.Password;
    }

    private void ConfirmPinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfirmNewPin = ConfirmPinBox.Password;
    }

    private void SyncPasswords()
    {
        _viewModel.CurrentPin = CurrentPinBox.Password;
        _viewModel.NewPin = NewPinBox.Password;
        _viewModel.ConfirmNewPin = ConfirmPinBox.Password;
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        SyncPasswords();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SyncPasswords();
            if (_viewModel.ChangePinCommand.CanExecute(null))
            {
                _viewModel.ChangePinCommand.Execute(null);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
