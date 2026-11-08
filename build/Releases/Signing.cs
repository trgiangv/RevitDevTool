using Build.Options;
using Build.Pipeline;
using DevTools.Update.Releases;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Build.Releases;

/// <summary>
///     Signs update feeds with ECDSA P-256 / SHA-256. Each <c>&lt;feed&gt;.sig</c> holds the base64 IEEE P1363 signature (0051).
/// </summary>
public static class Signing
{
    public static string Key() => new ConfigurationBuilder()
        .AddUserSecrets<BuildRun>()
        .AddEnvironmentVariables()
        .Build()
        .GetSection("Update")
        .Get<UpdateOptions>()?
        .SigningKey is { Length: > 0 } key
        ? key
        : throw new InvalidOperationException("Set Update__SigningKey to the ECDSA P-256 private key (PEM) that signs the update feeds.");

    public static string Sign(byte[] data, string privateKeyPem)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        if (ecdsa.KeySize != 256) throw new InvalidOperationException($"Update__SigningKey must be an ECDSA P-256 key, got {ecdsa.KeySize} bits.");

        return Convert.ToBase64String(ecdsa.SignData(data, HashAlgorithmName.SHA256));
    }

    public static void SignFile(string path, string privateKeyPem) =>
        File.WriteAllText(path + ReleaseSignature.Extension, Sign(File.ReadAllBytes(path), privateKeyPem));
}
