using Build.Options;
using DevTools.Update.Releases;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using System.Security.Cryptography;

namespace Build.Modules;

/// <summary>
///     Sign the update manifest and the Velopack release feeds with ECDSA P-256 / SHA-256.
///     Each <c>&lt;feed&gt;.sig</c> holds the base64 IEEE P1363 signature, the default format of <see cref="ECDsa.VerifyData(byte[], byte[], HashAlgorithmName)"/>.
/// </summary>
/// <remarks>
///     Skipped with a warning when <see cref="UpdateOptions.SigningKey"/> is empty, unless <see cref="UpdateOptions.RequireSignature"/> is set.
/// </remarks>
/// <seealso href="../../docs/decisions/0051-update-integrity-and-trust.md">0051</seealso>
[DependsOn<GenerateUpdateManifestModule>]
[DependsOn<PackDaemonModule>(Optional = true)]
[UsedImplicitly]
public sealed class SignUpdateFeedsModule(IOptions<BuildOptions> buildOptions, IOptions<UpdateOptions> updateOptions) : Module<string[]>
{
    protected override async Task<string[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var outputFolder = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        foreach (var stale in Directory.GetFiles(outputFolder.Path, $"*{ReleaseSignature.Extension}")) File.Delete(stale);

        var key = updateOptions.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            if (updateOptions.Value.RequireSignature)
            {
                throw new InvalidOperationException("Update__SigningKey is required to publish a release (Update__RequireSignature is true).");
            }

            context.Logger.LogWarning("Update__SigningKey is not set; update feeds are not signed");
            return [];
        }

        var feeds = new List<string> { Path.Combine(outputFolder.Path, UpdateManifest.FileName) };
        feeds.AddRange(Directory.GetFiles(outputFolder.Path, "releases.*.json"));

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(key);
        if (ecdsa.KeySize != 256)
        {
            throw new InvalidOperationException($"Update__SigningKey must be an ECDSA P-256 key, got {ecdsa.KeySize} bits.");
        }

        var signatures = new List<string>();
        foreach (var feed in feeds.Where(File.Exists))
        {
            var data = await File.ReadAllBytesAsync(feed, cancellationToken);
            var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);
            var signaturePath = feed + ReleaseSignature.Extension;
            await File.WriteAllTextAsync(signaturePath, Convert.ToBase64String(signature), cancellationToken);
            signatures.Add(signaturePath);
        }

        context.Summary.KeyValue("Artifacts", "Signed feeds", string.Join(", ", signatures.Select(Path.GetFileName)));
        return signatures.ToArray();
    }
}
