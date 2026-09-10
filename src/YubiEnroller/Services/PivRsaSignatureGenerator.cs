using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Yubico.YubiKey.Cryptography;
using Yubico.YubiKey.Piv;
using SysPublicKey = System.Security.Cryptography.X509Certificates.PublicKey;

namespace YubiEnroller.Services;

public class PivRsaSignatureGenerator : X509SignatureGenerator
{
    private readonly PivSession _pivSession;
    private readonly byte _slot;
    private readonly SysPublicKey _publicKey;
    private readonly int _keySizeBits;

    public PivRsaSignatureGenerator(PivSession pivSession, byte slot, SysPublicKey publicKey, int keySizeBits = 2048)
    {
        _pivSession = pivSession;
        _slot = slot;
        _publicKey = publicKey;
        _keySizeBits = keySizeBits;
    }

    protected override SysPublicKey BuildPublicKey() => _publicKey;

    public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm)
    {
        if (hashAlgorithm == HashAlgorithmName.SHA256)
        {
            // OID 1.2.840.113549.1.1.11 (sha256WithRSAEncryption) DER SEQUENCE
            return new byte[] { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B, 0x05, 0x00 };
        }
        if (hashAlgorithm == HashAlgorithmName.SHA1)
        {
            // OID 1.2.840.113549.1.1.5 (sha1WithRSAEncryption)
            return new byte[] { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x05, 0x05, 0x00 };
        }
        throw new NotSupportedException($"Hash algorithm {hashAlgorithm.Name} is not supported.");
    }

    public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm)
    {
        int digestAlgo = hashAlgorithm == HashAlgorithmName.SHA256 ? RsaFormat.Sha256 : RsaFormat.Sha1;
        byte[] digest = hashAlgorithm == HashAlgorithmName.SHA256 ? SHA256.HashData(data) : SHA1.HashData(data);
        byte[] formattedToSign = RsaFormat.FormatPkcs1Sign(digest, digestAlgo, _keySizeBits);

        return _pivSession.Sign(_slot, formattedToSign);
    }
}
