using System.Windows;
using YubiEnroller.Models;

namespace YubiEnroller.Views;

public partial class SettingsDialog : Window
{
    private readonly AppSettings _settings;

    public bool SettingsSaved { get; private set; }

    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        CaConfigBox.Text = _settings.CaConfigString;
        TemplateBox.Text = _settings.CertificateTemplate;
        SimulatorCheckBox.IsChecked = _settings.SimulatorMode;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.CaConfigString = CaConfigBox.Text.Trim();
        _settings.CertificateTemplate = string.IsNullOrWhiteSpace(TemplateBox.Text) ? "SmartcardLogon" : TemplateBox.Text.Trim();
        _settings.SimulatorMode = SimulatorCheckBox.IsChecked == true;
        _settings.Save();

        SettingsSaved = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
