using System;
using System.Windows;
using YubiEnroller.Services;

namespace YubiEnroller;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load configuration so AppLogger.IsEnabled is configured according to settings.json
        var settings = YubiEnroller.Models.AppSettings.Load();

        AppLogger.Info("==================================================");
        AppLogger.Info($"YubiEnroller started. Process ID: {Environment.ProcessId}");
        AppLogger.Info($"Executable Path: {Environment.ProcessPath}");
        AppLogger.Info($"OS Version: {Environment.OSVersion}");
        AppLogger.Info($"Runtime: {Environment.Version}");
        AppLogger.Info("==================================================");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            AppLogger.Error("CRITICAL: AppDomain unhandled exception", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            AppLogger.Error("CRITICAL: UI Dispatcher unhandled exception", args.Exception);
        };

        // Check for CLI execution mode
        if (e.Args.Length > 0)
        {
            var cli = CliHandler.ParseArgs(e.Args);
            if (cli.IsSilent || cli.ShowHelp || cli.Pin != null || cli.OnBehalfOf != null)
            {
                int exitCode = await CliHandler.RunAsync(e.Args);
                Shutdown(exitCode);
                return;
            }
        }

        // Standard GUI mode
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info($"YubiEnroller exiting with code {e.ApplicationExitCode}.");
        base.OnExit(e);
    }
}

