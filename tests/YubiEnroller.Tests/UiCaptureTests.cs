using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using YubiEnroller.Models;
using YubiEnroller.Services;
using YubiEnroller.ViewModels;
using YubiEnroller.Views;

namespace YubiEnroller.Tests;

public class UiCaptureTests
{
    private const string ArtifactDir = @"C:\Users\home-user\.gemini\antigravity-ide\brain\e0c8d47b-7217-407e-855b-e95703f91da1";

    [Fact]
    public void CaptureUiScreenshots()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new YubiEnroller.App
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                    app.InitializeComponent();
                }

                LocalizationService.Instance.SetLanguage("en");

                var settings = new AppSettings { SimulatorMode = true, Language = "en" };
                var simService = new YubiKeySimulatorService();
                var caService = new WindowsCaEnrollmentService();
                var hwService = new YubiKeyHardwareService();
                string docsDir = @"c:\apps\yubi-enroller\docs\screenshots";
                Directory.CreateDirectory(docsDir);

                // 1. Capture Empty State (No Certificate, Device Connected)
                {
                    var vm = new MainViewModel(hwService, simService, caService, settings);
                    var win = new MainWindow(settings)
                    {
                        DataContext = vm,
                        Width = 820,
                        Height = 620,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    win.Show();
                    SaveWindowToPng(win, "screenshot_empty_state.png", docsDir);
                    win.Close();
                }

                // 2. Capture Active Enrolled State (Slot 9a Enrolled)
                {
                    simService.SeedSampleCertificate();
                    var vm = new MainViewModel(hwService, simService, caService, settings);
                    var win = new MainWindow(settings)
                    {
                        DataContext = vm,
                        Width = 820,
                        Height = 620,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    win.Show();
                    SaveWindowToPng(win, "screenshot_enrolled_state.png", docsDir);
                    win.Close();
                }

                // 3. Capture Change PIN Dialog
                {
                    var pinVm = new ChangePinViewModel(simService);
                    var dialog = new ChangePinDialog(pinVm)
                    {
                        Width = 465,
                        Height = 550,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    dialog.Show();
                    SaveWindowToPng(dialog, "screenshot_change_pin.png", docsDir);
                    dialog.Close();
                }

                // 4. Capture Clean Enroll Dialog
                {
                    var enrollVm = new EnrollViewModel(simService, caService, settings);
                    var dialog = new EnrollDialog(enrollVm)
                    {
                        Width = 480,
                        Height = 470,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    dialog.Show();
                    SaveWindowToPng(dialog, "screenshot_enroll_dialog.png", docsDir);
                    dialog.Close();
                }

                // 5. Capture Settings Dialog (with hidden simulator mode)
                {
                    var settingsDialog = new SettingsDialog(settings)
                    {
                        Width = 520,
                        Height = 680,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    settingsDialog.Show();
                    SaveWindowToPng(settingsDialog, "screenshot_settings_dialog.png", docsDir);
                    settingsDialog.Close();
                }

                // 6. Capture Pure Physical Mode (No Device Attached)
                {
                    var prodSettings = new AppSettings { SimulatorMode = false, Language = "en" };
                    var disconnectedHw = new DisconnectedService();
                    var vm = new MainViewModel(disconnectedHw, simService, caService, prodSettings);
                    var win = new MainWindow(prodSettings)
                    {
                        DataContext = vm,
                        Width = 820,
                        Height = 620,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    win.Show();
                    SaveWindowToPng(win, "screenshot_no_device.png", docsDir);
                    win.Close();
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
                File.WriteAllText(Path.Combine(ArtifactDir, "ui_capture_error.txt"), ex.ToString());
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(15000);
        if (threadEx != null) throw new AggregateException("UI Capture failed", threadEx);
    }

    private static void SaveWindowToPng(Window window, string filename, string docsDir)
    {
        string p1 = Path.Combine(ArtifactDir, filename);
        RenderWindowToPng(window, p1);
        try
        {
            string p2 = Path.Combine(docsDir, filename);
            File.Copy(p1, p2, true);
        }
        catch { }
    }

    private static void RenderWindowToPng(Window window, string outputPath)
    {
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(new Size(window.Width, window.Height)));
        window.UpdateLayout();

        int width = (int)window.ActualWidth;
        int height = (int)window.ActualHeight;
        if (width <= 0) width = (int)window.Width;
        if (height <= 0) height = (int)window.Height;

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var fs = File.Create(outputPath);
        encoder.Save(fs);
    }
}

class DisconnectedService : IYubiKeyService
{
    public event EventHandler<DeviceTelemetry?>? DeviceStateChanged { add { } remove { } }
    public event EventHandler? CertificateChanged { add { } remove { } }
    public event EventHandler<bool>? TouchRequired { add { } remove { } }

    public DeviceTelemetry? CurrentDevice => null;
    public bool IsConnected => false;
    public bool IsSimulator => false;

    public CertificateModel? GetEnrolledCertificate(byte slot = 0x9A) => null;

    public Task<string> GenerateCsrAsync(byte slot, string subjectDn, string? upn, string keyType, string pin, string touchPolicy = "Default") =>
        throw new NotImplementedException();

    public Task<bool> InstallCertificateAsync(byte slot, byte[] certRawData, string pin) =>
        Task.FromResult(false);

    public Task<(bool Success, int? RetriesRemaining, string? ErrorMessage)> ChangePinAsync(string currentPin, string newPin) =>
        Task.FromResult<(bool, int?, string?)>((false, 0, "No device connected"));

    public Task<bool> DeleteCertificateAsync(byte slot, string pin) =>
        Task.FromResult(false);

    public int GetPinRetries() => 0;

    public void Refresh() { }
    public void Dispose() { }
}

