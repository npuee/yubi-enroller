using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YubiEnroller.Models;
using YubiEnroller.Services;

namespace YubiEnroller.Views;

public partial class SettingsDialog : Window
{
    private readonly AppSettings _settings;
    private bool _initializing = true;

    public bool SettingsSaved { get; private set; }

    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        LanguageComboBox.ItemsSource = LocalizationService.Instance.AvailableLanguages;
        LanguageComboBox.SelectedItem = LocalizationService.Instance.AvailableLanguages
            .FirstOrDefault(l => l.Code.Equals(_settings.Language, System.StringComparison.OrdinalIgnoreCase))
            ?? LocalizationService.Instance.CurrentLanguage;

        CaConfigBox.Text = _settings.CaConfigString;
        TemplateBox.Text = _settings.CertificateTemplate;
        SimulatorCheckBox.IsChecked = _settings.SimulatorMode;

        _initializing = false;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;

        if (LanguageComboBox.SelectedItem is LanguageItem selected)
        {
            LocalizationService.Instance.SetLanguage(selected.Code);
            _settings.Language = selected.Code;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is LanguageItem selected)
        {
            _settings.Language = selected.Code;
        }

        _settings.CaConfigString = CaConfigBox.Text.Trim();
        _settings.CertificateTemplate = string.IsNullOrWhiteSpace(TemplateBox.Text) ? "SmartcardLogon" : TemplateBox.Text.Trim();
        _settings.SimulatorMode = SimulatorCheckBox.IsChecked == true;
        _settings.Save();

        SettingsSaved = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // Revert language if user cancelled
        LocalizationService.Instance.SetLanguage(_settings.Language);
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CancelButton_Click(sender, e);
    }

    private void OpenLogFileButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.OpenLogFile();
    }

    private void OpenSettingsFileButton_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.OpenSettingsFile();
    }
}
