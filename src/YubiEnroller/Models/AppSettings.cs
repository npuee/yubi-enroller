using System;
using System.IO;
using System.Text.Json;

namespace YubiEnroller.Models;

public class AppSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YubiEnroller");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDir, "settings.json");

    public string CaConfigString { get; set; } = string.Empty;
    public string CertificateTemplate { get; set; } = "SmartcardLogon";
    public byte DefaultSlot { get; set; } = 0x9A;
    public bool SimulatorMode { get; set; } = false;
    public string DefaultKeyAlgorithm { get; set; } = "RSA2048";

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}
