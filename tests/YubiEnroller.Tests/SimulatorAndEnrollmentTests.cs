using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Xunit;
using YubiEnroller.Models;
using YubiEnroller.Services;
using YubiEnroller.ViewModels;

namespace YubiEnroller.Tests;

public class SimulatorAndEnrollmentTests
{
    [Fact]
    public void Simulator_InitialTelemetry_IsCorrect()
    {
        using var sim = new YubiKeySimulatorService();

        Assert.True(sim.IsConnected);
        Assert.True(sim.IsSimulator);

        var device = sim.CurrentDevice;
        Assert.NotNull(device);
        Assert.Equal(19482012, device.SerialNumber);
        Assert.Equal("19482012", device.DisplaySerial);
        Assert.Contains("YubiKey 5 NFC", device.ModelName);
        Assert.Equal("5.4.3", device.FirmwareVersion);
        Assert.Equal(3, device.PinRetriesRemaining);
    }

    [Fact]
    public void Simulator_Initially_HasNoCertificate()
    {
        using var sim = new YubiKeySimulatorService();
        var cert = sim.GetEnrolledCertificate(0x9A);
        Assert.Null(cert);
    }

    [Fact]
    public async Task Simulator_ChangePin_DecrementsOnWrongPin()
    {
        using var sim = new YubiKeySimulatorService();

        var result = await sim.ChangePinAsync("wrong-pin", "654321");
        Assert.False(result.Success);
        Assert.Equal(2, result.RetriesRemaining);
        Assert.Equal(2, sim.GetPinRetries());
    }

    [Fact]
    public async Task Simulator_ChangePin_SucceedsWithCorrectPin()
    {
        using var sim = new YubiKeySimulatorService();

        var result = await sim.ChangePinAsync("123456", "654321");
        Assert.True(result.Success);
        Assert.Equal(3, result.RetriesRemaining);

        // Verify new pin works
        var secondResult = await sim.ChangePinAsync("654321", "123456");
        Assert.True(secondResult.Success);
    }

    [Fact]
    public async Task Simulator_GenerateCsr_ProducesValidPem()
    {
        using var sim = new YubiKeySimulatorService();

        string csr = await sim.GenerateCsrAsync(
            0x9A,
            "CN=Alice Admin, OU=Security, DC=corp, DC=local",
            "alice@corp.local",
            "RSA2048",
            "123456");

        Assert.NotNull(csr);
        Assert.StartsWith("-----BEGIN CERTIFICATE REQUEST-----", csr.Trim());
        Assert.EndsWith("-----END CERTIFICATE REQUEST-----", csr.Trim());
    }

    [Fact]
    public async Task Simulator_EnrollmentLifecycle_FullFlow()
    {
        using var sim = new YubiKeySimulatorService();
        var caService = new WindowsCaEnrollmentService();

        // 1. Generate CSR
        string csr = await sim.GenerateCsrAsync(
            0x9A,
            "CN=Bob Test, DC=corp, DC=local",
            "bob@corp.local",
            "RSA2048",
            "123456");

        // 2. Submit to CA (simulator mode)
        var enrollResult = await caService.SubmitCsrAsync(
            csr,
            "SmartcardLogon",
            "",
            isSimulatorMode: true);

        Assert.True(enrollResult.Success);
        Assert.NotNull(enrollResult.CertificateBytes);

        // 3. Install on YubiKey
        bool installed = await sim.InstallCertificateAsync(
            0x9A,
            enrollResult.CertificateBytes,
            "123456");

        Assert.True(installed);

        // 4. Verify certificate is now enrolled
        var enrolledCert = sim.GetEnrolledCertificate(0x9A);
        Assert.NotNull(enrolledCert);
        Assert.Contains(Environment.UserName, enrolledCert.CommonName);
        Assert.Equal("Slot 9a (Authentication / Smart Card Logon)", enrolledCert.SlotName);
        Assert.False(enrolledCert.IsExpired);
        Assert.Contains("Smart Card Logon", enrolledCert.EnhancedKeyUsages);
    }

    [Fact]
    public void CertificateModel_ExtractsDetailsProperly()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=Test Cert, OU=IT, DC=domain, DC=com",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddUserPrincipalName("user@domain.com");
        req.CertificateExtensions.Add(san.Build());

        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(364));

        var model = CertificateModel.FromX509Certificate2(cert, 0x9A);

        Assert.Equal("Test Cert", model.CommonName);
        Assert.Equal("user@domain.com", model.UserPrincipalName);
        Assert.Equal("RSA", model.KeyAlgorithm);
        Assert.Equal(2048, model.KeySize);
        Assert.False(model.IsExpired);
        Assert.True(model.DaysRemaining > 300);
        Assert.Contains("Valid", model.StatusBadgeText);
    }

    [Fact]
    public async Task ChangePinViewModel_Validations_TriggerExpectedMessages()
    {
        using var sim = new YubiKeySimulatorService();
        var vm = new ChangePinViewModel(sim);

        string? lastTitle = null;
        string? lastMsg = null;
        bool? lastIsError = null;
        bool closed = false;

        vm.RequestShowMessage += (t, m, err) =>
        {
            lastTitle = t;
            lastMsg = m;
            lastIsError = err;
        };
        vm.RequestClose += () => closed = true;

        // 1. Missing current PIN
        vm.CurrentPin = "";
        vm.NewPin = "654321";
        vm.ConfirmNewPin = "654321";
        await vm.ExecuteChangePinAsync();
        Assert.Equal("Missing Current PIN", lastTitle);
        Assert.True(lastIsError);
        Assert.False(closed);

        // 2. PIN too short
        vm.CurrentPin = "123456";
        vm.NewPin = "12345";
        vm.ConfirmNewPin = "12345";
        await vm.ExecuteChangePinAsync();
        Assert.Equal("Invalid PIN Length", lastTitle);
        Assert.True(lastIsError);
        Assert.False(closed);

        // 3. PIN mismatch
        vm.CurrentPin = "123456";
        vm.NewPin = "654321";
        vm.ConfirmNewPin = "654322";
        await vm.ExecuteChangePinAsync();
        Assert.Equal("PIN Mismatch", lastTitle);
        Assert.True(lastIsError);
        Assert.False(closed);

        // 4. Incorrect current PIN
        vm.CurrentPin = "999999";
        vm.NewPin = "654321";
        vm.ConfirmNewPin = "654321";
        await vm.ExecuteChangePinAsync();
        Assert.Equal("PIN Change Failed", lastTitle);
        Assert.True(lastIsError);
        Assert.False(closed);
        Assert.Equal(2, vm.RetriesRemaining);

        // 5. Successful change
        vm.CurrentPin = "123456";
        vm.NewPin = "654321";
        vm.ConfirmNewPin = "654321";
        await vm.ExecuteChangePinAsync();
        Assert.Equal("PIN Changed Successfully", lastTitle);
        Assert.False(lastIsError);
        Assert.True(closed);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public void AppSettings_ResolvesExternalPath_AndSerializesCorrectly()
    {
        string path = AppSettings.SettingsFilePath;
        Assert.NotNull(path);
        Assert.EndsWith("settings.json", path);

        var settings = new AppSettings
        {
            Language = "de",
            CaConfigString = "corp-ca.domain.local\\Issuing-CA",
            CertificateTemplate = "CustomSmartcard"
        };
        settings.Save();

        var loaded = AppSettings.Load();
        Assert.Equal("de", loaded.Language);
        Assert.Equal("corp-ca.domain.local\\Issuing-CA", loaded.CaConfigString);
        Assert.Equal("CustomSmartcard", loaded.CertificateTemplate);

        // Reset to default English
        new AppSettings { Language = "en" }.Save();
        LocalizationService.Instance.SetLanguage("en");
    }

    [Fact]
    public void LocalizationService_LoadsLanguages_AndSwitchesDynamically()
    {
        var loc = LocalizationService.Instance;
        Assert.NotEmpty(loc.AvailableLanguages);
        Assert.Contains(loc.AvailableLanguages, l => l.Code == "en");
        Assert.Contains(loc.AvailableLanguages, l => l.Code == "de");
        Assert.Contains(loc.AvailableLanguages, l => l.Code == "fr");
        Assert.Contains(loc.AvailableLanguages, l => l.Code == "es");
        Assert.Contains(loc.AvailableLanguages, l => l.Code == "lt");

        // Switch to English
        loc.SetLanguage("en");
        Assert.Equal("Enroll Certificate", loc["Empty_BtnEnroll"]);
        Assert.Equal("Change PIN", loc["Empty_BtnChangePin"]);

        // Switch to German
        loc.SetLanguage("de");
        Assert.Equal("Zertifikat registrieren", loc["Empty_BtnEnroll"]);
        Assert.Equal("PIN ändern", loc["Empty_BtnChangePin"]);

        // Switch to French
        loc.SetLanguage("fr");
        Assert.Equal("Inscrire un certificat", loc["Empty_BtnEnroll"]);
        Assert.Equal("Modifier le code PIN", loc["Empty_BtnChangePin"]);

        // Switch to Spanish
        loc.SetLanguage("es");
        Assert.Equal("Inscribir certificado", loc["Empty_BtnEnroll"]);
        Assert.Equal("Cambiar PIN", loc["Empty_BtnChangePin"]);

        // Switch to Lithuanian
        loc.SetLanguage("lt");
        Assert.Equal("Užsakyti sertifikatą", loc["Empty_BtnEnroll"]);
        Assert.Equal("Keisti PIN", loc["Empty_BtnChangePin"]);

        // Switch to Estonian
        loc.SetLanguage("et");
        Assert.Equal("Registreeri sertifikaat", loc["Empty_BtnEnroll"]);
        Assert.Equal("Muuda PIN-koodi", loc["Empty_BtnChangePin"]);

        // Revert to English
        loc.SetLanguage("en");
    }

    [Fact]
    public void EnrollViewModel_LoadsTemplatesFromSettings()
    {
        var settings = new AppSettings
        {
            CertificateTemplates = new List<string> { "EnterpriseSmartcard", "CustomLogon" },
            CertificateTemplate = "CustomLogon"
        };
        var simService = new YubiKeySimulatorService();
        var caService = new WindowsCaEnrollmentService();
        var vm = new EnrollViewModel(simService, caService, settings);

        Assert.Equal(2, vm.AvailableTemplates.Count);
        Assert.Contains("EnterpriseSmartcard", vm.AvailableTemplates);
        Assert.Contains("CustomLogon", vm.AvailableTemplates);
        Assert.Equal("CustomLogon", vm.SelectedTemplate);
    }

    [Fact]
    public async Task EnrollViewModel_DefaultPin_BlocksAndPromptsChange()
    {
        var settings = new AppSettings { CertificateTemplates = new List<string> { "SmartcardLogon" } };
        var simService = new YubiKeySimulatorService();
        var caService = new WindowsCaEnrollmentService();
        var vm = new EnrollViewModel(simService, caService, settings)
        {
            Pin = "123456" // Default factory PIN
        };

        bool promptTriggered = false;
        bool openChangePinTriggered = false;

        vm.RequestDefaultPinChange += () =>
        {
            promptTriggered = true;
            return Task.FromResult(true); // user chooses Yes
        };

        vm.RequestOpenChangePin += () =>
        {
            openChangePinTriggered = true;
        };

        await vm.StartEnrollmentAsync();

        Assert.True(promptTriggered);
        Assert.True(openChangePinTriggered);
        Assert.False(vm.IsEnrolling);
        Assert.False(vm.IsComplete);
    }

    [Fact]
    public void AppSettings_EnableLogging_DefaultsToFalse_AndSupportsAliases()
    {
        var settings = new AppSettings();
        Assert.False(settings.EnableLogging);

        // Deserializing with EnableLogging: true
        string json1 = "{\"EnableLogging\": true}";
        var loaded1 = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json1);
        Assert.NotNull(loaded1);
        Assert.True(loaded1.EnableLogging);

        // Deserializing with Logging: true alias
        string json2 = "{\"Logging\": true}";
        var loaded2 = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json2);
        Assert.NotNull(loaded2);
        Assert.True(loaded2.EnableLogging);

        // Deserializing with LogEnabled: true alias
        string json3 = "{\"LogEnabled\": true}";
        var loaded3 = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json3);
        Assert.NotNull(loaded3);
        Assert.True(loaded3.EnableLogging);
    }

    [Fact]
    public void AppLogger_OffByDefault_OnlyWritesWhenEnabled()
    {
        AppLogger.IsEnabled = false;
        Assert.False(AppLogger.IsEnabled);

        string testMarkerDisabled = $"TEST_DISABLED_{Guid.NewGuid():N}";
        AppLogger.Info(testMarkerDisabled);

        string logPath = AppLogger.LogFilePath;
        if (File.Exists(logPath))
        {
            string content = File.ReadAllText(logPath);
            Assert.DoesNotContain(testMarkerDisabled, content);
        }

        // Enable logging
        AppLogger.IsEnabled = true;
        Assert.True(AppLogger.IsEnabled);

        string testMarkerEnabled = $"TEST_ENABLED_{Guid.NewGuid():N}";
        AppLogger.Info(testMarkerEnabled);

        Assert.True(File.Exists(logPath));
        string updatedContent = File.ReadAllText(logPath);
        Assert.Contains(testMarkerEnabled, updatedContent);

        // Clean up: turn back off
        AppLogger.IsEnabled = false;
    }

    [Fact]
    public void CliHandler_ParseArgs_ParsesAllFlagsCorrectly()
    {
        string[] args = new[]
        {
            "--silent",
            "--simulator",
            "--on-behalf-of", @"CORP\jdoe",
            "--template", "EnterpriseSmartcard",
            "--pin", "123456",
            "--new-pin", "654321",
            "--ca", @"ca01.corp.local\Corp-CA",
            "--touch-policy", "Always",
            "--slot", "0x9A"
        };

        var opts = CliHandler.ParseArgs(args);

        Assert.True(opts.IsSilent);
        Assert.True(opts.UseSimulator);
        Assert.Equal(@"CORP\jdoe", opts.OnBehalfOf);
        Assert.Equal("EnterpriseSmartcard", opts.Template);
        Assert.Equal("123456", opts.Pin);
        Assert.Equal("654321", opts.NewPin);
        Assert.Equal(@"ca01.corp.local\Corp-CA", opts.CaConfig);
        Assert.Equal("Always", opts.TouchPolicy);
        Assert.Equal((byte)0x9A, opts.Slot);
    }

    [Fact]
    public async Task CliHandler_RunAsync_MissingPin_ReturnsExitCode1()
    {
        string[] args = new[] { "--silent", "--simulator" };
        int exitCode = await CliHandler.RunAsync(args);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task CliHandler_RunAsync_Simulator_FullSilentFlow_ReturnsExitCode0()
    {
        string[] args = new[]
        {
            "--silent",
            "--simulator",
            "--pin", "123456",
            "--template", "SmartcardLogon"
        };

        int exitCode = await CliHandler.RunAsync(args);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task CliHandler_RunAsync_Simulator_EnrollOnBehalfOf_ReturnsExitCode0()
    {
        string[] args = new[]
        {
            "--silent",
            "--simulator",
            "--pin", "123456",
            "--on-behalf-of", @"CONTOSO\bob_contractor",
            "--template", "SmartcardUser"
        };

        int exitCode = await CliHandler.RunAsync(args);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Simulator_TouchRequired_EventFires_WhenTouchPolicySpecified()
    {
        using var sim = new YubiKeySimulatorService();
        bool touchRequested = false;
        bool touchReleased = false;

        sim.TouchRequired += (sender, isRequired) =>
        {
            if (isRequired) touchRequested = true;
            else touchReleased = true;
        };

        string csr = await sim.GenerateCsrAsync(
            0x9A,
            "CN=TouchTester",
            "touch@domain.local",
            "RSA2048",
            "123456",
            touchPolicy: "Always");

        Assert.NotNull(csr);
        Assert.True(touchRequested, "TouchRequired(true) should have fired for Always touch policy.");
        Assert.True(touchReleased, "TouchRequired(false) should have fired after simulated touch.");
    }

    [Fact]
    public async Task EnrollViewModel_EnrollOnBehalfOf_UpdatesTargetIdentityAndEnrolls()
    {
        var settings = new AppSettings
        {
            CertificateTemplates = new List<string> { "SmartcardLogon" },
            EnrollmentAgentMode = true
        };
        var simService = new YubiKeySimulatorService();
        // Update PIN away from default so security check in EnrollViewModel passes
        await simService.ChangePinAsync("123456", "654321");

        var caService = new WindowsCaEnrollmentService();

        var vm = new EnrollViewModel(simService, caService, settings)
        {
            Pin = "654321", // non-default pin
            TargetUsername = @"CONTOSO\alice.specialist"
        };

        Assert.True(vm.IsEnrollmentAgentMode);
        Assert.Equal("alice.specialist", vm.SubjectCommonName);
        Assert.Contains("alice.specialist@contoso", vm.UserPrincipalName);

        // Run enrollment
        await vm.StartEnrollmentAsync();

        Assert.False(vm.HasError);
        Assert.True(vm.IsComplete);

        var cert = simService.GetEnrolledCertificate(0x9A);
        Assert.NotNull(cert);
        Assert.Contains("alice.specialist", cert.CommonName);
    }
}


