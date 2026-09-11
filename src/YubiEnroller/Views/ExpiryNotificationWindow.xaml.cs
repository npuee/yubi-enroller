using System;
using System.Windows;
using System.Windows.Media;
using YubiEnroller.Models;
using YubiEnroller.Services;

namespace YubiEnroller.Views;

public partial class ExpiryNotificationWindow : Window
{
    public bool RenewRequested { get; private set; }

    public Brush BorderColorBrush { get; }
    public Brush IconBackgroundBrush { get; }
    public Brush HeaderForegroundBrush { get; }
    public string StatusIcon { get; }
    public string HeaderText { get; }
    public string SubtitleText { get; }
    public string MessageBodyText { get; }
    public string DeviceMetaText { get; }

    public ExpiryNotificationWindow(CertificateModel certificate, DeviceTelemetry? device, int daysRemaining)
    {
        DataContext = this;

        bool isExpired = certificate.IsExpired || daysRemaining <= 0;

        if (isExpired)
        {
            BorderColorBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Red
            IconBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x13, 0x15));
            HeaderForegroundBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
            StatusIcon = "🔴";
            HeaderText = LocalizationService.Get("ExpiryNotify_ExpiredHeader");
            SubtitleText = "Smart Card Authentication Blocked";
            string expiryDateStr = certificate.NotAfter.ToLocalTime().ToString("yyyy-MM-dd");
            MessageBodyText = string.Format(LocalizationService.Get("ExpiryNotify_ExpiredBody"), expiryDateStr);
        }
        else
        {
            BorderColorBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Amber
            IconBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x26, 0x08));
            HeaderForegroundBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
            StatusIcon = "⚠️";
            HeaderText = LocalizationService.Get("ExpiryNotify_ExpiringHeader");
            SubtitleText = "Slot 9a (Authentication)";
            string expiryDateStr = certificate.NotAfter.ToLocalTime().ToString("yyyy-MM-dd");
            MessageBodyText = string.Format(LocalizationService.Get("ExpiryNotify_ExpiringBody"), daysRemaining, expiryDateStr);
        }

        string deviceName = device != null ? $"{device.ModelName} (SN: {device.SerialNumber})" : "YubiKey";
        string userIdent = !string.IsNullOrWhiteSpace(certificate.UserPrincipalName)
            ? certificate.UserPrincipalName
            : certificate.CommonName;
        DeviceMetaText = $"{deviceName} • {userIdent}";

        InitializeComponent();

        Loaded += (s, e) =>
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Bottom - ActualHeight - 20;
        };
    }

    private void RenewButton_Click(object sender, RoutedEventArgs e)
    {
        RenewRequested = true;
        Close();
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        RenewRequested = false;
        Close();
    }
}
