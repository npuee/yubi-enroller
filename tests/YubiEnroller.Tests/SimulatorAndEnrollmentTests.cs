using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Xunit;
using YubiEnroller.Models;
using YubiEnroller.Services;

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
}
