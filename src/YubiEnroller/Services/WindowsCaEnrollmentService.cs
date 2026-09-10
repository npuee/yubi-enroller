using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YubiEnroller.Models;

namespace YubiEnroller.Services;

public class EnrollmentResult
{
    public bool Success { get; set; }
    public byte[]? CertificateBytes { get; set; }
    public CertificateModel? Certificate { get; set; }
    public int? RequestId { get; set; }
    public bool IsPendingApproval { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RawOutput { get; set; }
}

public class WindowsCaEnrollmentService
{
    public async Task<EnrollmentResult> SubmitCsrAsync(
        string csrPem,
        string templateName,
        string caConfigString,
        bool isSimulatorMode = false)
    {
        if (isSimulatorMode)
        {
            return await SimulateCaIssuanceAsync(csrPem, templateName);
        }

        return await Task.Run(() => SubmitViaCertReq(csrPem, templateName, caConfigString));
    }

    private EnrollmentResult SubmitViaCertReq(string csrPem, string templateName, string caConfigString)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "YubiEnroller_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string csrPath = Path.Combine(tempDir, "request.csr");
        string cerPath = Path.Combine(tempDir, "response.cer");
        string p7bPath = Path.Combine(tempDir, "response.p7b");

        try
        {
            File.WriteAllText(csrPath, csrPem);

            string args = BuildCertReqArgs(csrPath, cerPath, p7bPath, templateName, caConfigString);
            AppLogger.Info($"WindowsCA: Submitting CSR via certreq.exe with args: {args}");

            var psi = new ProcessStartInfo
            {
                FileName = "certreq.exe",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                AppLogger.Error("WindowsCA: Failed to start certreq.exe process.");
                return new EnrollmentResult
                {
                    Success = false,
                    Message = "Failed to start certreq.exe process."
                };
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30000); // 30 sec timeout

            string combinedOutput = stdout + Environment.NewLine + stderr;
            AppLogger.Info($"WindowsCA: certreq.exe completed (exit code={process.ExitCode}). Output:\n{combinedOutput.Trim()}");

            // Check if certificate was issued
            if (File.Exists(cerPath) && new FileInfo(cerPath).Length > 0)
            {
                byte[] rawData = File.ReadAllBytes(cerPath);
                var cert = new X509Certificate2(rawData);
                AppLogger.Info($"WindowsCA: Certificate issued successfully. Subject='{cert.Subject}', Serial='{cert.SerialNumber}', Thumbprint='{cert.Thumbprint}'");
                return new EnrollmentResult
                {
                    Success = true,
                    CertificateBytes = rawData,
                    Certificate = CertificateModel.FromX509Certificate2(cert),
                    Message = "Certificate issued successfully by Windows CA.",
                    RawOutput = combinedOutput
                };
            }

            // Check if taken under submission (pending manager approval)
            if (combinedOutput.Contains("Taken Under Submission", StringComparison.OrdinalIgnoreCase) ||
                combinedOutput.Contains("Certificate request is pending", StringComparison.OrdinalIgnoreCase))
            {
                int? reqId = ExtractRequestId(combinedOutput);
                return new EnrollmentResult
                {
                    Success = false,
                    IsPendingApproval = true,
                    RequestId = reqId,
                    Message = $"Certificate request was submitted to CA and is pending approval (Request ID: {reqId?.ToString() ?? "Unknown"}).",
                    RawOutput = combinedOutput
                };
            }

            return new EnrollmentResult
            {
                Success = false,
                Message = $"Windows CA enrollment failed: {ExtractErrorMessage(combinedOutput)}",
                RawOutput = combinedOutput
            };
        }
        catch (Exception ex)
        {
            return new EnrollmentResult
            {
                Success = false,
                Message = $"Error executing certreq: {ex.Message}"
            };
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private string BuildCertReqArgs(string csrPath, string cerPath, string p7bPath, string templateName, string caConfigString)
    {
        var args = "-submit -q";

        if (!string.IsNullOrWhiteSpace(caConfigString))
        {
            args += $" -config \"{caConfigString.Trim()}\"";
        }

        if (!string.IsNullOrWhiteSpace(templateName))
        {
            args += $" -attrib \"CertificateTemplate:{templateName.Trim()}\"";
        }

        args += $" \"{csrPath}\" \"{cerPath}\" \"{p7bPath}\"";
        return args;
    }

    private static int? ExtractRequestId(string output)
    {
        var match = Regex.Match(output, @"RequestId:\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int id))
        {
            return id;
        }
        return null;
    }

    private static string ExtractErrorMessage(string output)
    {
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("0x", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Denied", StringComparison.OrdinalIgnoreCase))
            {
                return line.Trim();
            }
        }
        return lines.Length > 0 ? lines[^1].Trim() : "Unknown CA response error.";
    }

    private async Task<EnrollmentResult> SimulateCaIssuanceAsync(string csrPem, string templateName)
    {
        await Task.Delay(1200); // Simulate network latency

        try
        {
            // Parse public key from CSR
            var csrBytes = Convert.FromBase64String(
                csrPem.Replace("-----BEGIN CERTIFICATE REQUEST-----", "")
                      .Replace("-----END CERTIFICATE REQUEST-----", "")
                      .Replace("\r", "")
                      .Replace("\n", "")
                      .Trim());

            // Create self-signed or simulated CA certificate
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(
                "CN=" + Environment.UserName + ", OU=Users, DC=corp, DC=local",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var san = new SubjectAlternativeNameBuilder();
            san.AddUserPrincipalName($"{Environment.UserName}@{Environment.UserDomainName.ToLowerInvariant()}.local");
            req.CertificateExtensions.Add(san.Build());

            var eku = new OidCollection
            {
                new Oid("1.3.6.1.4.1.311.20.2.2", "Smart Card Logon"),
                new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication")
            };
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));

            var cert = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(1));

            var certModel = CertificateModel.FromX509Certificate2(cert);
            certModel.Issuer = "CN=Corporate-Enterprise-CA, DC=corp, DC=local";
            certModel.IssuerCommonName = "Corporate-Enterprise-CA";

            return new EnrollmentResult
            {
                Success = true,
                CertificateBytes = cert.RawData,
                Certificate = certModel,
                Message = $"Simulated enrollment succeeded with template '{templateName}'.",
                RawOutput = "Certificate retrieved(Issued) Issued"
            };
        }
        catch (Exception ex)
        {
            return new EnrollmentResult
            {
                Success = false,
                Message = $"Simulation error: {ex.Message}"
            };
        }
    }
}
