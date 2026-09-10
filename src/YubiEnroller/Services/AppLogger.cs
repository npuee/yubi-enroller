using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace YubiEnroller.Services;

public static class AppLogger
{
    private static readonly object _fileLock = new();
    private static string _logFilePath = string.Empty;

    public static string LogFilePath
    {
        get
        {
            if (string.IsNullOrEmpty(_logFilePath))
            {
                InitializeLogPath();
            }
            return _logFilePath;
        }
    }

    private static void InitializeLogPath()
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

            string primaryPath = Path.Combine(baseDir, "yubi-enroller.log");

            // Test if directory is writable
            try
            {
                using var fs = File.Open(primaryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                _logFilePath = primaryPath;
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback to local app data if running from a protected folder (e.g., Program Files)
                string fallbackDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YubiEnroller");
                Directory.CreateDirectory(fallbackDir);
                _logFilePath = Path.Combine(fallbackDir, "yubi-enroller.log");
            }
        }
        catch
        {
            _logFilePath = Path.Combine(Path.GetTempPath(), "yubi-enroller.log");
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        string fullMessage = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        Log("ERROR", fullMessage);
    }
    public static void Debug(string message) => Log("DEBUG", message);

    private static void Log(string level, string message)
    {
        try
        {
            int threadId = Environment.CurrentManagedThreadId;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"{timestamp} [{level,-5}] [T{threadId:D2}] {message}{Environment.NewLine}";

            System.Diagnostics.Debug.Write(line);

            lock (_fileLock)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Logging must never throw or disrupt application flow
        }
    }

    public static void OpenLogFile()
    {
        try
        {
            if (File.Exists(LogFilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogFilePath,
                    UseShellExecute = true
                });
            }
            else
            {
                string dir = Path.GetDirectoryName(LogFilePath) ?? AppContext.BaseDirectory;
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }
}
