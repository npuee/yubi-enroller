using System.Windows;
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
