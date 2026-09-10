using System.Windows;
using YubiEnroller.Models;

namespace YubiEnroller.Views;

public partial class CertDetailsDialog : Window
{
    private readonly CertificateModel _certificate;

    public CertDetailsDialog(CertificateModel certificate)
    {
        InitializeComponent();
        _certificate = certificate;
        DataContext = _certificate;
    }

    private void CopyPem_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_certificate.RawPem);
        MessageBox.Show(this, "Certificate PEM copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
