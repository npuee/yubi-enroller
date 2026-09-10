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

                var settings = new AppSettings { SimulatorMode = true };
                var simService = new YubiKeySimulatorService();
                var caService = new WindowsCaEnrollmentService();
                var hwService = new YubiKeyHardwareService();

                // 1. Capture Empty State (No Certificate, Device Connected)
                {
                    var vm = new MainViewModel(hwService, simService, caService, settings);
                    var win = new MainWindow
                    {
                        DataContext = vm,
                        Width = 820,
                        Height = 620,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    win.Show();
                    RenderWindowToPng(win, Path.Combine(ArtifactDir, "screenshot_empty_state.png"));
                    win.Close();
                }

                // 2. Capture Enrolled State (Active Certificate)
                {
                    simService.SeedSampleCertificate();
                    var vm = new MainViewModel(hwService, simService, caService, settings);
                    var win = new MainWindow
                    {
                        DataContext = vm,
                        Width = 820,
                        Height = 620,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    win.Show();
                    RenderWindowToPng(win, Path.Combine(ArtifactDir, "screenshot_enrolled_state.png"));
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
                    RenderWindowToPng(dialog, Path.Combine(ArtifactDir, "screenshot_change_pin.png"));
                    dialog.Close();
                }

                // 4. Capture Enroll Dialog
                {
                    var enrollVm = new EnrollViewModel(simService, caService, settings);
                    var dialog = new EnrollDialog(enrollVm)
                    {
                        Width = 520,
                        Height = 680,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    dialog.Show();
                    RenderWindowToPng(dialog, Path.Combine(ArtifactDir, "screenshot_enroll_dialog.png"));
                    dialog.Close();
                }

                // 5. Capture Settings Dialog (with hidden simulator mode)
                {
                    var settingsDialog = new SettingsDialog(settings)
                    {
                        Width = 480,
                        Height = 480,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    settingsDialog.Show();
                    RenderWindowToPng(settingsDialog, Path.Combine(ArtifactDir, "screenshot_settings_dialog.png"));
                    settingsDialog.Close();
                }

                // 6. Capture Pure Physical Mode (No Device Attached)
                {
                    var prodSettings = new AppSettings { SimulatorMode = false };
                    var vm = new MainViewModel(hwService, simService, caService, prodSettings);
                    var win = new MainWindow
                    {
                        DataContext = vm,
                        Width = 820,
                        Height = 620,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -2000,
                        Top = -2000
                    };
                    win.Show();
                    RenderWindowToPng(win, Path.Combine(ArtifactDir, "screenshot_no_device.png"));
                    win.Close();
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(ArtifactDir, "ui_capture_error.txt"), ex.ToString());
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(15000);
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
