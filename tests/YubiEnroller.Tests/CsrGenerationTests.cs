using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using Xunit.Abstractions;

namespace YubiEnroller.Tests;

public class CsrGenerationTests
{
    private readonly ITestOutputHelper _output;

    public CsrGenerationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestStandardRsaCsrGeneration()
    {
        using var rsa = RSA.Create(2048);
        var subject = new X500DistinguishedName("CN=TestUser, OU=IT, DC=corp, DC=local");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Add extensions e.g. Subject Alternative Name (UPN) or Template
        var builder = new SubjectAlternativeNameBuilder();
        builder.AddUserPrincipalName("testuser@corp.local");
        request.CertificateExtensions.Add(builder.Build());

        byte[] csrDer = request.CreateSigningRequest();
        string csrPem = PemEncoding.WriteString("CERTIFICATE REQUEST", csrDer);

        _output.WriteLine("Generated CSR:\n" + csrPem);
        Assert.StartsWith("-----BEGIN CERTIFICATE REQUEST-----", csrPem);
    }
}
