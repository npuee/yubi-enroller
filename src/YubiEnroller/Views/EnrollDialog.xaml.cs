using System.Windows;
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
