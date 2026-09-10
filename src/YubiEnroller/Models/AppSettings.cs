using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
        defaultSettings.Save(); // create initial settings.json
        return defaultSettings;
    }

    public void Save()
    {
        try
        {
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
