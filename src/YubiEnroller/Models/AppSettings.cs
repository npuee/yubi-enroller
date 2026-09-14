using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using YubiEnroller.Services;

namespace YubiEnroller.Models;

public class AppSettings
{
    private static string? _resolvedSettingsFilePath;

    public string Language { get; set; } = "en";
    public string CaConfigString { get; set; } = string.Empty;
    public string CertificateTemplate { get; set; } = "SmartcardLogon";
    public System.Collections.Generic.List<string> CertificateTemplates { get; set; } = new()
    {
        "SmartcardLogon",
        "SmartcardUser",
        "User",
        "ClientAuth"
    };
    public byte DefaultSlot { get; set; } = 0x9A;
    public bool SimulatorMode { get; set; } = false;
    public string DefaultKeyAlgorithm { get; set; } = "RSA2048";
    public bool EnrollmentAgentMode { get; set; } = false;
    public string DefaultTouchPolicy { get; set; } = "Default";

    private int _notificationDaysBeforeExpiry = 30;

    public int NotificationDaysBeforeExpiry
    {
        get => _notificationDaysBeforeExpiry;
        set => _notificationDaysBeforeExpiry = value > 0 ? value : 30;
    }

    [JsonPropertyName("ExpiryNotificationDays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpiryNotificationDaysAlias
    {
        get => null;
        set
        {
            if (value.HasValue && value.Value > 0) _notificationDaysBeforeExpiry = value.Value;
        }
    }

    [JsonPropertyName("NotificationDays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NotificationDaysAlias
    {
        get => null;
        set
        {
            if (value.HasValue && value.Value > 0) _notificationDaysBeforeExpiry = value.Value;
        }
    }

    private bool _enableLogging = false;

    public bool EnableLogging
    {
        get => _enableLogging;
        set => _enableLogging = value;
    }

    [JsonPropertyName("Logging")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LoggingAlias
    {
        get => null;
        set
        {
            if (value.HasValue) _enableLogging = value.Value;
        }
    }

    [JsonPropertyName("LogEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LogEnabledAlias
    {
        get => null;
        set
        {
            if (value.HasValue) _enableLogging = value.Value;
        }
    }

    private bool _blockPukOnEnrollment = true;

    public bool BlockPukOnEnrollment
    {
        get => _blockPukOnEnrollment;
        set => _blockPukOnEnrollment = value;
    }

    [JsonPropertyName("BlockPuk")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BlockPukAlias
    {
        get => null;
        set
        {
            if (value.HasValue) _blockPukOnEnrollment = value.Value;
        }
    }

    [JsonPropertyName("DisablePuk")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DisablePukAlias
    {
        get => null;
        set
        {
            if (value.HasValue) _blockPukOnEnrollment = value.Value;
        }
    }

    public static string SettingsFilePath
    {
        get
        {
            if (string.IsNullOrEmpty(_resolvedSettingsFilePath))
            {
                _resolvedSettingsFilePath = DetermineSettingsFilePath();
            }
            return _resolvedSettingsFilePath;
        }
    }

    private static string DetermineSettingsFilePath()
    {
        try
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

            string primaryPath = Path.Combine(baseDir, "settings.json");

            // Test if folder is writable
            try
            {
                using var fs = File.Open(primaryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                return primaryPath;
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback to local app data if running from a protected directory (e.g., Program Files)
                string fallbackDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YubiEnroller");
                Directory.CreateDirectory(fallbackDir);
                return Path.Combine(fallbackDir, "settings.json");
            }
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            string path = SettingsFilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    AppLogger.IsEnabled = settings.EnableLogging;
                    AppLogger.Info($"AppSettings: Loaded configuration from '{path}'.");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"AppSettings: Failed to load configuration: {ex.Message}. Using defaults.");
        }

        var defaultSettings = new AppSettings();
        AppLogger.IsEnabled = defaultSettings.EnableLogging;
        defaultSettings.Save(); // create initial settings.json
        return defaultSettings;
    }

    public void Save()
    {
        try
        {
            AppLogger.IsEnabled = EnableLogging;
            string path = SettingsFilePath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            AppLogger.Info($"AppSettings: Saved configuration to '{path}'.");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"AppSettings: Failed to save configuration", ex);
        }
    }

    public static void OpenSettingsFile()
    {
        try
        {
            string path = SettingsFilePath;
            if (!File.Exists(path))
            {
                new AppSettings().Save();
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("AppSettings: Failed to open settings file", ex);
        }
    }
}
