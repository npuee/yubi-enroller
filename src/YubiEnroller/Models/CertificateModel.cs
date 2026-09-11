using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace YubiEnroller.Models;

public class CertificateModel
{
    public byte Slot { get; set; } = 0x9A;
    public string SlotName { get; set; } = "Slot 9a (Authentication)";
    public string Subject { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string? UserPrincipalName { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string IssuerCommonName { get; set; } = string.Empty;
    public string SerialNumberHex { get; set; } = string.Empty;
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public string ThumbprintSha256 { get; set; } = string.Empty;
    public string ThumbprintSha1 { get; set; } = string.Empty;
    public string Thumbprint => ThumbprintSha1;
    public string KeyAlgorithm { get; set; } = "RSA";
    public int KeySize { get; set; } = 2048;
    public List<string> EnhancedKeyUsages { get; set; } = new();
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();

    public int ExpiryWarningDays { get; set; } = 30;
    public bool IsExpired => DateTime.UtcNow > NotAfter.ToUniversalTime();
    public int DaysRemaining => Math.Max(0, (int)(NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays);
    public bool IsExpiringSoon => !IsExpired && DaysRemaining <= ExpiryWarningDays;

    public string StatusBadgeText
    {
        get
        {
            if (IsExpired) return "Expired";
            if (IsExpiringSoon) return $"Expiring Soon ({DaysRemaining}d remaining)";
            return $"Valid ({DaysRemaining}d remaining)";
        }
    }

    public string RawPem => PemEncoding.WriteString("CERTIFICATE", RawBytes);

    public static CertificateModel FromX509Certificate2(X509Certificate2 cert, byte slot = 0x9A, int expiryWarningDays = 30)
    {
        var model = new CertificateModel
        {
            Slot = slot,
            ExpiryWarningDays = expiryWarningDays,
            SlotName = slot switch
            {
                0x9A => "Slot 9a (Authentication / Smart Card Logon)",
                0x9C => "Slot 9c (Digital Signature)",
                0x9D => "Slot 9d (Key Management)",
                0x9E => "Slot 9e (Card Authentication)",
                _ => $"Slot {slot:X2}"
            },
            Subject = cert.Subject,
            Issuer = cert.Issuer,
            SerialNumberHex = cert.SerialNumber,
            NotBefore = cert.NotBefore,
            NotAfter = cert.NotAfter,
            ThumbprintSha1 = cert.Thumbprint,
            RawBytes = cert.RawData
        };

        // Extract Common Name from Subject
        model.CommonName = ExtractRdnValue(cert.Subject, "CN=") ?? cert.Subject;
        model.IssuerCommonName = ExtractRdnValue(cert.Issuer, "CN=") ?? cert.Issuer;

        // SHA-256 Thumbprint
        model.ThumbprintSha256 = Convert.ToHexString(SHA256.HashData(cert.RawData));

        // Key info
        if (cert.GetRSAPublicKey() is { } rsa)
        {
            model.KeyAlgorithm = "RSA";
            model.KeySize = rsa.KeySize;
        }
        else if (cert.GetECDsaPublicKey() is { } ecdsa)
        {
            model.KeyAlgorithm = "ECDSA";
            model.KeySize = ecdsa.KeySize;
        }

        // Extended Key Usages
        foreach (var ext in cert.Extensions)
        {
            if (ext is X509EnhancedKeyUsageExtension eku)
            {
                foreach (var oid in eku.EnhancedKeyUsages)
                {
                    model.EnhancedKeyUsages.Add(oid.FriendlyName ?? oid.Value ?? "Unknown EKU");
                }
            }
            else if (ext.Oid?.Value == "2.5.29.17") // Subject Alternative Name
            {
                // Try parse UPN
                try
                {
                    var raw = ext.Format(false);
                    var match = System.Text.RegularExpressions.Regex.Match(
                        raw,
                        @"(?:Principal Name|UPN)\s*=\s*([^,;\r\n]+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        model.UserPrincipalName = match.Groups[1].Value.Trim();
                    }
                }
                catch { }
            }
        }

        return model;
    }

    private static string? ExtractRdnValue(string dn, string prefix)
    {
        if (string.IsNullOrEmpty(dn)) return null;
        var parts = dn.Split(',');
        foreach (var p in parts)
        {
            var trimmed = p.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[prefix.Length..].Trim();
            }
        }
        return null;
    }
}
