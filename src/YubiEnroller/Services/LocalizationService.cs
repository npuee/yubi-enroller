using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace YubiEnroller.Services;

public class LanguageItem
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = string.Empty;

    public string FullDisplay => string.IsNullOrEmpty(FlagEmoji) ? DisplayName : $"{FlagEmoji} {DisplayName}";

    public override string ToString() => FullDisplay;
}

public class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private LanguageItem _currentLanguage;
    private List<LanguageItem> _availableLanguages = new();

    public IReadOnlyList<LanguageItem> AvailableLanguages => _availableLanguages;

    public LanguageItem CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (value != null && _currentLanguage?.Code != value.Code)
            {
                SetLanguage(value.Code);
            }
        }
    }

    public string this[string key]
    {
        get => Get(key);
        set { /* No-op to support TwoWay binding controls like Run.Text */ }
    }

    public static string Get(string key) => Instance.GetString(key);

    private LocalizationService()
    {
        InitializeAvailableLanguages();

        // Default to English
        _currentLanguage = _availableLanguages.FirstOrDefault(l => l.Code == "en") ?? _availableLanguages[0];
        LoadLanguage(_currentLanguage.Code);
    }

    private void InitializeAvailableLanguages()
    {
        var defaults = new List<LanguageItem>
        {
            new() { Code = "en", DisplayName = "English", FlagEmoji = "🇬🇧" },
            new() { Code = "et", DisplayName = "Eesti", FlagEmoji = "🇪🇪" },
            new() { Code = "de", DisplayName = "Deutsch", FlagEmoji = "🇩🇪" },
            new() { Code = "fr", DisplayName = "Français", FlagEmoji = "🇫🇷" },
            new() { Code = "es", DisplayName = "Español", FlagEmoji = "🇪🇸" },
            new() { Code = "lt", DisplayName = "Lietuvių", FlagEmoji = "🇱🇹" }
        };

        // Scan for additional language files in locales/ folder
        try
        {
            string localesDir = GetLocalesDirectory();
            if (Directory.Exists(localesDir))
            {
                foreach (string file in Directory.GetFiles(localesDir, "*.json"))
                {
                    string code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (!defaults.Any(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                    {
                        defaults.Add(new LanguageItem
                        {
                            Code = code,
                            DisplayName = code.ToUpperInvariant(),
                            FlagEmoji = "🌐"
                        });
                    }
                }
            }
        }
        catch { }

        _availableLanguages = defaults;
    }

    public static string GetLocalesDirectory()
    {
        string baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir) && Environment.ProcessPath != null)
        {
            baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
        }
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
        }
        return Path.Combine(baseDir, "locales");
    }

    public void SetLanguage(string langCode)
    {
        var matched = _availableLanguages.FirstOrDefault(l => l.Code.Equals(langCode, StringComparison.OrdinalIgnoreCase));
        if (matched == null)
        {
            matched = _availableLanguages.FirstOrDefault(l => l.Code == "en") ?? _availableLanguages[0];
        }

        _currentLanguage = matched;
        LoadLanguage(matched.Code);

        AppLogger.Info($"LocalizationService: Active language switched to '{matched.DisplayName}' ({matched.Code}).");

        // Notify all WPF bindings to refresh
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    private void LoadLanguage(string langCode)
    {
        _strings.Clear();

        // 1. Load built-in fallback baseline
        foreach (var (k, v) in BuiltInDefaults)
        {
            _strings[k] = v;
        }

        // 2. Load from locales/{langCode}.json file if present
        try
        {
            string localesDir = GetLocalesDirectory();
            string filePath = Path.Combine(localesDir, $"{langCode}.json");

            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        _strings[kvp.Key] = kvp.Value;
                    }
                    AppLogger.Info($"LocalizationService: Loaded {dict.Count} strings from '{filePath}'.");
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"LocalizationService: Error loading language '{langCode}': {ex.Message}");
        }
    }

    public string GetString(string key)
    {
        if (_strings.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
        {
            return val;
        }
        if (BuiltInDefaults.TryGetValue(key, out var defaultVal))
        {
            return defaultVal;
        }
        return key;
    }

    // Built-in English baseline
    private static readonly Dictionary<string, string> BuiltInDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        // Window & Header
        ["App_Title"] = "YubiEnroller - YubiKey PIV Certificate Manager",
        ["Header_Subtitle"] = "PIV Smart Card Certificate Enrollment & Management",
        ["Header_SimulatorBadge"] = "SIMULATOR MODE",
        ["Header_HardwareBadge"] = "YUBIKEY CONNECTED",
        ["Header_NoDeviceBadge"] = "NO DEVICE DETECTED",
        ["Header_SettingsTooltip"] = "Settings & Configuration",
        ["Header_RefreshTooltip"] = "Scan / Refresh Hardware",

        // Main Card - Enrolled
        ["Card_EnrolledTitle"] = "Active Authentication Certificate",
        ["Card_SlotPrefix"] = "PIV Slot 9a (Authentication)",
        ["Card_SubjectLabel"] = "Subject",
        ["Card_UpnLabel"] = "UPN",
        ["Card_IssuerLabel"] = "Issued By",
        ["Card_KeyTypeLabel"] = "Key Specs",
        ["Card_ThumbprintLabel"] = "Thumbprint",
        ["Card_ValidPill"] = "Valid",
        ["Card_ExpiredPill"] = "Expired",
        ["Card_DaysRemaining"] = "{0}d remaining",
        ["Card_BtnRenew"] = "Renew / Re-enroll",
        ["Card_BtnChangePin"] = "Change PIN",
        ["Card_BtnDetails"] = "View Details",
        ["Card_BtnExport"] = "Export (.cer)",
        ["Card_BtnDelete"] = "Delete Key",

        // Main Card - Empty State
        ["Empty_Title"] = "No Certificate Enrolled",
        ["Empty_Description"] = "This YubiKey does not have an active certificate in PIV Slot 9a. Enroll a certificate from your Active Directory Certificate Authority (AD CS) to enable Smart Card Logon, VPN, and secure enterprise authentication.",
        ["Empty_BtnEnroll"] = "Enroll Certificate",
        ["Empty_BtnChangePin"] = "Change PIN",

        // Main Card - No Device State
        ["NoDevice_Title"] = "No YubiKey Detected",
        ["NoDevice_Description"] = "Please insert your YubiKey into a USB port to manage smart card certificates and security PINs. You can also enable Simulator Mode in Settings for diagnostic testing.",
        ["NoDevice_BtnSettings"] = "Open Settings",

        // Taskbar / Telemetry
        ["Taskbar_NoDevice"] = "No Device",
        ["Taskbar_SerialPrefix"] = "SN:",
        ["Taskbar_FirmwarePrefix"] = "FW:",
        ["Taskbar_PinRetriesPrefix"] = "PIN Retries:",
        ["Taskbar_CaPrefix"] = "CA:",
        ["Taskbar_CaAuto"] = "CA: Active Directory (Auto)",

        // Change PIN Dialog
        ["PinDialog_Title"] = "Change PIV PIN",
        ["PinDialog_Subtitle"] = "YubiKey Security Access",
        ["PinDialog_BannerText"] = "PIN protects your private keys and smart card certificates.",
        ["PinDialog_BannerDefault"] = "Factory default PIN is 123456. Must be 6 to 8 characters.",
        ["PinDialog_CurrentPin"] = "Current PIN",
        ["PinDialog_RetriesLeft"] = "Retries Left:",
        ["PinDialog_NewPin"] = "New PIN (6 to 8 characters)",
        ["PinDialog_ConfirmPin"] = "Confirm New PIN",
        ["PinDialog_BtnCancel"] = "Cancel",
        ["PinDialog_BtnUpdate"] = "Update PIN",
        ["PinDialog_BtnUpdating"] = "Updating PIN...",
        ["PinDialog_MissingCurrent"] = "Please enter your current PIN.",
        ["PinDialog_MissingCurrentTitle"] = "Missing Current PIN",
        ["PinDialog_InvalidLength"] = "New PIN must be between 6 and 8 characters.",
        ["PinDialog_InvalidLengthTitle"] = "Invalid PIN Length",
        ["PinDialog_Mismatch"] = "New PIN and confirmation do not match.",
        ["PinDialog_MismatchTitle"] = "PIN Mismatch",
        ["PinDialog_SuccessTitle"] = "PIN Changed Successfully",
        ["PinDialog_SuccessMessage"] = "Your YubiKey PIV PIN has been successfully updated!",
        ["PinDialog_FailTitle"] = "PIN Change Failed",

        // Enroll Dialog
        ["EnrollDialog_Title"] = "Enroll PIV Certificate",
        ["EnrollDialog_Subtitle"] = "Active Directory Certificate Services (AD CS)",
        ["EnrollDialog_Step1"] = "1. Identity",
        ["EnrollDialog_Step2"] = "2. Template",
        ["EnrollDialog_Step3"] = "3. Security PIN",
        ["EnrollDialog_Step4"] = "4. Enrollment",
        ["EnrollDialog_IdentityTitle"] = "Windows Logon Identity",
        ["EnrollDialog_IdentityFixedBadge"] = "Locked",
        ["EnrollDialog_IdentityHint"] = "Automatically bound to your current Windows logon session.",
        ["EnrollDialog_CommonNameLabel"] = "Subject Common Name (CN)",
        ["EnrollDialog_UpnLabel"] = "User Principal Name (UPN / Email)",
        ["EnrollDialog_TemplateLabel"] = "Certificate Template",
        ["EnrollDialog_KeyTypeLabel"] = "Key Algorithm & Size",
        ["EnrollDialog_CaServerLabel"] = "CA Server (Optional)",
        ["EnrollDialog_PinPrompt"] = "Enter YubiKey PIN to authorize on-chip key generation",
        ["EnrollDialog_BtnBack"] = "Back",
        ["EnrollDialog_BtnNext"] = "Next Step",
        ["EnrollDialog_BtnSubmit"] = "Submit & Enroll Certificate",
        ["EnrollDialog_BtnEnrolling"] = "Enrolling...",
        ["EnrollDialog_EnrollingFor"] = "Enrolling for user: {0}",
        ["EnrollDialog_DefaultPinTitle"] = "Default Factory PIN Detected",
        ["EnrollDialog_DefaultPinPrompt"] = "Your YubiKey is currently using the factory default PIN (123456). For security reasons, you must set a new personal PIN before enrolling a smart card certificate.\n\nWould you like to change your PIN now?",
        ["EnrollDialog_DefaultPinError"] = "Factory default PIN (123456) is not allowed. Please change your PIN before enrolling.",

        // Certificate Details Dialog
        ["CertDetails_Title"] = "Certificate Details",
        ["CertDetails_Subtitle"] = "X.509 PIV Slot 9a (Authentication)",
        ["CertDetails_TabOverview"] = "Overview",
        ["CertDetails_TabDetails"] = "Certificate Details",
        ["CertDetails_Subject"] = "Subject",
        ["CertDetails_Issuer"] = "Issuer",
        ["CertDetails_ValidFrom"] = "Valid From",
        ["CertDetails_ValidTo"] = "Valid To",
        ["CertDetails_SerialNumber"] = "Serial Number",
        ["CertDetails_KeyUsage"] = "Key Usage",
        ["CertDetails_Eku"] = "Enhanced Key Usages",
        ["CertDetails_BtnExportPem"] = "Export PEM",
        ["CertDetails_BtnClose"] = "Close",

        // Settings Dialog
        ["Settings_Title"] = "Application Settings",
        ["Settings_Subtitle"] = "Windows CA & Hardware Preferences",
        ["Settings_LanguageLabel"] = "User Interface Language",
        ["Settings_CaServerLabel"] = "Active Directory CA Server (Config String)",
        ["Settings_CaServerHint"] = "Example: ca01.domain.local\\Enterprise-Root-CA (leave blank for auto-discovery)",
        ["Settings_TemplateLabel"] = "Default Certificate Template",
        ["Settings_TemplateHint"] = "Standard templates: SmartcardLogon, SmartcardUser, User.",
        ["Settings_AdvancedTitle"] = "ADVANCED / DIAGNOSTICS",
        ["Settings_SimulatorCheck"] = "Enable Simulator Mode (Virtual YubiKey 5 NFC)",
        ["Settings_SimulatorHint"] = "Simulates on-chip keys and enrollment when hardware is not plugged in.",
        ["Settings_LogsTitle"] = "Real-Time Diagnostics Log",
        ["Settings_LogsHint"] = "Hardware & CA events written to yubi-enroller.log",
        ["Settings_BtnOpenLog"] = "Open Log File",
        ["Settings_ConfigTitle"] = "Configuration File",
        ["Settings_ConfigHint"] = "External settings file in settings.json",
        ["Settings_BtnOpenConfig"] = "Open Settings File",
        ["Settings_BtnCancel"] = "Cancel",
        ["Settings_BtnSave"] = "Save Settings"
    };
}
