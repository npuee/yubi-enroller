using System;
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
}


