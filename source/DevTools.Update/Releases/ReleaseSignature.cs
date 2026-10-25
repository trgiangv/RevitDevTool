using System.Security.Cryptography;

namespace DevTools.Update.Releases;

/// <summary>
///     Verifies <c>&lt;asset&gt;.sig</c>: base64 IEEE P1363 ECDSA P-256 / SHA-256 of the asset bytes (0051).
/// </summary>
public sealed class ReleaseSignature
{
    public const string Extension = ".sig";

    private readonly ECDsa? _key;

    public ReleaseSignature(string? publicKeyPem)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem)) return;

        _key = ECDsa.Create();
        _key.ImportFromPem(publicKeyPem);
        if (_key.KeySize != 256)
            throw new ArgumentException($"The update public key must be ECDSA P-256. It has {_key.KeySize} bits.", nameof(publicKeyPem));
    }

    /// <summary>
    ///     False if there is no public key. Then the engine accepts assets without a signature.
    /// </summary>
    public bool IsRequired => _key is not null;

    public void Verify(string assetName, byte[] data, byte[] signatureBase64)
    {
        if (_key is null) return;

        if (!_key.VerifyData(data, Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(signatureBase64)), HashAlgorithmName.SHA256))
            throw new UpdateIntegrityException($"The signature of {assetName} is not correct.");
    }
}
