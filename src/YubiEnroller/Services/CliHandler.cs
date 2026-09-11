using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using YubiEnroller.Models;

namespace YubiEnroller.Services;

public class CliOptions
{
    public bool IsSilent { get; set; }
    public bool ShowHelp { get; set; }
    public bool UseSimulator { get; set; }
    public string? OnBehalfOf { get; set; }
    public string? Template { get; set; }
    public string? Pin { get; set; }
    public string? NewPin { get; set; }
    public string? CaConfig { get; set; }
    public string TouchPolicy { get; set; } = "Default";
    public byte Slot { get; set; } = 0x9A;
    public bool CheckExpiry { get; set; }
    public int? ExpiryDays { get; set; }
}

public static class CliHandler
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    public static void EnsureConsoleOutput()
    {
        try
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            var stdOut = Console.OpenStandardOutput();
            var stdErr = Console.OpenStandardError();
            if (stdOut != Stream.Null)
            {
                Console.SetOut(new StreamWriter(stdOut, Console.OutputEncoding) { AutoFlush = true });
            }
            if (stdErr != Stream.Null)
            {
                Console.SetError(new StreamWriter(stdErr, Console.OutputEncoding) { AutoFlush = true });
            }
        }
        catch
        {
            // Ignore if stream redirection fails in current context
        }
    }

    public static CliOptions ParseArgs(string[] args)
    {
        var options = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase) || arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                options.IsSilent = true;
            }
            else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("-?"))
            {
                options.ShowHelp = true;
            }
            else if (arg.Equals("--simulator", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSimulator = true;
            }
            else if ((arg.Equals("--on-behalf-of", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-u", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--target-user", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.OnBehalfOf = args[++i];
            }
            else if ((arg.Equals("--template", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-t", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--cert-template", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--certificate-template", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.Template = args[++i];
            }
            else if ((arg.Equals("--pin", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.Pin = args[++i];
            }
            else if (arg.Equals("--new-pin", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.NewPin = args[++i];
            }
            else if (arg.Equals("--ca", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CaConfig = args[++i];
            }
            else if (arg.Equals("--touch-policy", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.TouchPolicy = args[++i];
            }
            else if (arg.Equals("--check-expiry", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--notify-expiry", StringComparison.OrdinalIgnoreCase))
            {
                options.CheckExpiry = true;
            }
            else if ((arg.Equals("--days", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-d", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out int daysVal))
                {
                    options.ExpiryDays = daysVal;
                }
            }
            else if (arg.Equals("--slot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                string slotStr = args[++i];
                if (slotStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    options.Slot = Convert.ToByte(slotStr[2..], 16);
                }
                else if (byte.TryParse(slotStr, out byte slotVal))
                {
                    options.Slot = slotVal;
                }
            }
        }

        return options;
    }

    public static async Task<int> RunAsync(string[] args)
    {
        EnsureConsoleOutput();

        var options = ParseArgs(args);

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var settings = AppSettings.Load();
        if (!string.IsNullOrEmpty(settings.Language))
        {
            LocalizationService.Instance.SetLanguage(settings.Language);
        }

        if (options.CheckExpiry)
        {
            return await CheckExpiryFlowAsync(options, settings);
        }

        string template = options.Template ?? settings.CertificateTemplate ?? "SmartcardLogon";
        string caConfig = options.CaConfig ?? settings.CaConfigString;
        string touchPolicy = options.TouchPolicy ?? settings.DefaultTouchPolicy;
        byte slot = options.Slot != 0 ? options.Slot : settings.DefaultSlot;

        if (string.IsNullOrWhiteSpace(options.Pin))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[ERROR] Missing required --pin parameter for silent enrollment.");
            Console.ResetColor();
            Console.WriteLine("        Example: YubiEnroller.exe --silent --pin 123456");
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine("  YubiEnroller - Silent Enterprise Provisioning Engine");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        // Instantiate service
        IYubiKeyService service;
        bool isSimulator = options.UseSimulator || settings.SimulatorMode;

        if (isSimulator)
        {
            Console.WriteLine("[INFO] Using YubiKey Virtual Simulator Mode.");
            service = new YubiKeySimulatorService();
        }
        else
        {
            service = new YubiKeyHardwareService();
        }

        // Wait for device arrival if physical hardware
        if (!service.IsConnected)
        {
            Console.Write("[INFO] Waiting for YubiKey to be inserted...");
            int attempts = 10;
            while (!service.IsConnected && attempts-- > 0)
            {
                await Task.Delay(500);
                service.Refresh();
                Console.Write(".");
            }
            Console.WriteLine();
        }

        if (!service.IsConnected)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[ERROR] No YubiKey detected. Please insert token and retry.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[INFO] Connected: {service.CurrentDevice?.ModelName} (SN: {service.CurrentDevice?.SerialNumber}, FW: {service.CurrentDevice?.FirmwareVersion})");

        string currentPin = options.Pin;

        // Step 1: Change PIN if --new-pin requested
        if (!string.IsNullOrWhiteSpace(options.NewPin))
        {
            Console.WriteLine($"[INFO] Updating YubiKey PIN to new value...");
            var (pinSuccess, retriesRemaining, pinErr) = await service.ChangePinAsync(currentPin, options.NewPin);
            if (!pinSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"[ERROR] Failed to update PIN: {pinErr} (retries remaining: {retriesRemaining})");
                Console.ResetColor();
                return 1;
            }
            Console.WriteLine("[INFO] PIN updated successfully.");
            currentPin = options.NewPin;
        }

        // Step 2: Determine target identity (Enroll on Behalf Of vs Current User)
        string effectiveUser = Environment.UserName;
        string effectiveDomain = Environment.UserDomainName.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(options.OnBehalfOf))
        {
            string raw = options.OnBehalfOf.Trim();
            if (raw.Contains('\\'))
            {
                var parts = raw.Split('\\', 2);
                effectiveDomain = parts[0].ToLowerInvariant();
                effectiveUser = parts[1];
            }
            else if (raw.Contains('@'))
            {
                var parts = raw.Split('@', 2);
                effectiveUser = parts[0];
                effectiveDomain = parts[1].ToLowerInvariant();
            }
            else
            {
                effectiveUser = raw;
            }
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[INFO] ENROLL ON BEHALF OF: Target User = '{effectiveUser}' (Domain: {effectiveDomain})");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"[INFO] Enrolling for logged-in user: '{effectiveUser}' (Domain: {effectiveDomain})");
        }

        string subjectDn = $"CN={effectiveUser}";
        string upn = effectiveDomain.Contains('.') ? $"{effectiveUser}@{effectiveDomain}" : $"{effectiveUser}@{effectiveDomain}.local";

        // Step 3: Touch Sensor Listener
        service.TouchRequired += (s, isReq) =>
        {
            if (isReq)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ACTION REQUIRED] >>> PLEASE TOUCH YOUR YUBIKEY SENSOR NOW <<<");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("[INFO] Touch confirmed.");
            }
        };

        // Step 4: Generate On-Chip Key Pair & CSR
        Console.WriteLine($"[INFO] Generating on-chip {settings.DefaultKeyAlgorithm} keypair on slot 0x{slot:X2}...");
        string csrPem;
        try
        {
            csrPem = await service.GenerateCsrAsync(
                slot,
                subjectDn,
                upn,
                settings.DefaultKeyAlgorithm,
                currentPin,
                touchPolicy);
            Console.WriteLine("[INFO] CSR created and signed on-chip.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] Failed to generate on-chip keypair/CSR: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        // Step 5: Submit CSR to Active Directory CA
        Console.WriteLine($"[INFO] Submitting request to Windows CA (Template: '{template}')...");
        var caService = new WindowsCaEnrollmentService();
        var enrollmentResult = await caService.SubmitCsrAsync(
            csrPem,
            template,
            caConfig,
            service.IsSimulator,
            options.OnBehalfOf);

        if (enrollmentResult.IsPendingApproval)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[PENDING] Request submitted to CA and taken under submission (Request ID: {enrollmentResult.RequestId}).");
            Console.ResetColor();
            return 2;
        }

        if (!enrollmentResult.Success || enrollmentResult.CertificateBytes == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] CA enrollment failed: {enrollmentResult.Message}");
            Console.ResetColor();
            return 1;
        }

        // Step 6: Install Certificate in Slot 9a
        Console.WriteLine($"[INFO] Installing issued certificate into Slot 0x{slot:X2}...");
        try
        {
            bool installed = await service.InstallCertificateAsync(slot, enrollmentResult.CertificateBytes, currentPin);
            if (!installed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("[ERROR] Failed to write certificate to YubiKey Slot.");
                Console.ResetColor();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] Exception installing certificate: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("==================================================================");
        Console.WriteLine($"  SUCCESS! Certificate successfully enrolled and installed.");
        Console.WriteLine($"  Target User : {effectiveUser} ({upn})");
        Console.WriteLine($"  Issuer      : {enrollmentResult.Certificate?.IssuerCommonName ?? enrollmentResult.Certificate?.Issuer}");
        Console.WriteLine($"  Thumbprint  : {enrollmentResult.Certificate?.Thumbprint}");
        Console.WriteLine($"  Slot        : 0x{slot:X2}");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        return 0;
    }

    private static async Task<int> CheckExpiryFlowAsync(CliOptions options, AppSettings settings)
    {
        await Task.Yield();
        IYubiKeyService service;
        bool isSimulator = options.UseSimulator || settings.SimulatorMode;

        if (isSimulator)
        {
            service = new YubiKeySimulatorService();
        }
        else
        {
            service = new YubiKeyHardwareService();
        }

        if (!service.IsConnected)
        {
            Console.WriteLine("[INFO] No YubiKey connected. Expiry check skipped.");
            AppLogger.Info("CliHandler: Expiry check skipped, no token detected.");
            return 0;
        }

        byte slot = options.Slot != 0 ? options.Slot : settings.DefaultSlot;
        var cert = service.GetEnrolledCertificate(slot);

        if (cert == null)
        {
            Console.WriteLine($"[INFO] No certificate found in Slot 0x{slot:X2}. Skipping expiration check.");
            AppLogger.Info($"CliHandler: No certificate in slot 0x{slot:X2}.");
            return 0;
        }

        int thresholdDays = options.ExpiryDays ?? settings.NotificationDaysBeforeExpiry;
        int daysRemaining = cert.DaysRemaining;
        bool isExpired = cert.IsExpired || daysRemaining <= 0;
        bool isExpiringSoon = !isExpired && daysRemaining <= thresholdDays;

        Console.WriteLine($"[INFO] Certificate : {cert.Subject}");
        Console.WriteLine($"[INFO] Days Left   : {daysRemaining} (Warning threshold: {thresholdDays} days)");

        if (!isExpired && !isExpiringSoon)
        {
            Console.WriteLine($"[INFO] Certificate is healthy ({daysRemaining} days remaining). No notification needed.");
            AppLogger.Info($"CliHandler: Certificate healthy ({daysRemaining}d remaining, threshold: {thresholdDays}d).");
            return 0;
        }

        string alertType = isExpired ? "EXPIRED" : "EXPIRING SOON";
        Console.ForegroundColor = isExpired ? ConsoleColor.Red : ConsoleColor.Yellow;
        Console.WriteLine($"[ALERT] Certificate is {alertType}! ({daysRemaining} days remaining)");
        Console.ResetColor();
        AppLogger.Warn($"CliHandler: Certificate is {alertType}! ({daysRemaining} days remaining).");

        if (options.IsSilent)
        {
            // Headless compliance alert code: 10 indicates expiration warning
            return 10;
        }

        // Show interactive notification window on UI thread
        bool renew = false;
        var app = System.Windows.Application.Current;
        if (app != null)
        {
            app.Dispatcher.Invoke(() =>
            {
                var notifyWin = new Views.ExpiryNotificationWindow(cert, service.CurrentDevice, daysRemaining);
                notifyWin.ShowDialog();
                renew = notifyWin.RenewRequested;
            });
        }
        else
        {
            var thread = new System.Threading.Thread(() =>
            {
                var notifyWin = new Views.ExpiryNotificationWindow(cert, service.CurrentDevice, daysRemaining);
                notifyWin.ShowDialog();
                renew = notifyWin.RenewRequested;
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        if (renew)
        {
            Console.WriteLine("[INFO] User requested certificate renewal. Launching dashboard...");
            AppLogger.Info("CliHandler: User launched renewal from expiry notification.");
            if (app != null)
            {
                app.Dispatcher.Invoke(() =>
                {
                    var mainWin = new MainWindow(settings);
                    mainWin.ShowDialog();
                });
            }
            else
            {
                var mainThread = new System.Threading.Thread(() =>
                {
                    var mainWin = new MainWindow(settings);
                    mainWin.ShowDialog();
                });
                mainThread.SetApartmentState(System.Threading.ApartmentState.STA);
                mainThread.Start();
                mainThread.Join();
            }
        }

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
YubiEnroller CLI - Standalone YubiKey PIV Smart Card Enroller

USAGE:
  YubiEnroller.exe [OPTIONS]
  YubiEnroller.exe --silent --pin <PIN> [--on-behalf-of <USER>]
  YubiEnroller.exe --check-expiry [--days <DAYS>] [--silent]

OPTIONS:
  -s, --silent                  Run headless without GUI (silent batch provisioning / compliance check)
  -u, --on-behalf-of <USER>     Target user for Enroll on Behalf Of (e.g. DOMAIN\jsmith or jsmith@corp.local)
  -t, --template <NAME>         Certificate Template name (default: from settings.json)
  -p, --pin <PIN>               Current/factory YubiKey PIV PIN (required for silent enrollment)
  --new-pin <PIN>               Set new PIN during provisioning
  --ca <CONFIG>                 Active Directory CA config string (default: auto-discovery)
  --touch-policy <POLICY>       Touch policy: Default, Always, Cached, Never (default: Default)
  --slot <HEX>                  Target PIV Slot (default: 9A)
  --check-expiry                Check certificate expiration against threshold and alert if expiring
  -d, --days <DAYS>             Override expiration warning threshold in days (default: from settings.json)
  --simulator                   Force YubiKey Virtual Simulator mode for testing
  -h, --help                    Display this help message and exit

EXIT CODES:
  0   Success / Normal completion
  1   Error (Invalid parameters, PIN error, or CA failure)
  2   Pending Approval (CA request taken under submission)
  10  Certificate Expiring / Expired (Returned when running --check-expiry --silent)

EXAMPLES:
  # Standard silent enrollment for current user:
  YubiEnroller.exe --silent --pin 123456

  # Enroll on behalf of another user and update PIN:
  YubiEnroller.exe --silent --on-behalf-of CORP\jdoe --pin 123456 --new-pin 829104

  # Silent enrollment using specific CA template:
  YubiEnroller.exe --silent --template SmartcardUser --pin 123456

  # SCCM Scheduled Task: Check expiration and show alert popup if expiring within 30 days:
  YubiEnroller.exe --check-expiry

  # SCCM Scheduled Task: Check expiration with custom 14-day threshold:
  YubiEnroller.exe --check-expiry --days 14
");
    }
}
