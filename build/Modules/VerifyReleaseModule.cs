using Build.Options;
using DevTools.Update;
using DevTools.Update.Releases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Sourcy.DotNet;
using System.Security.Cryptography;
using SemanticVersion = Velopack.SemanticVersion;
using VelopackAssetFeed = Velopack.VelopackAssetFeed;

namespace Build.Modules;

/// <summary>
///     Checks the release assets the way a client reads them, before anything is uploaded (0054):
///     the manifest and the payload hashes, the Velopack feed and its packages, the signatures with the public key
///     that the Daemon ships, and that the version is newer than the previous tag.
/// </summary>
[DependsOn<ResolveVersionModule>]
[DependsOn<GenerateUpdateManifestModule>]
[DependsOn<PackDaemonModule>]
[DependsOn<SignUpdateFeedsModule>]
[UsedImplicitly]
public sealed class VerifyReleaseModule(IOptions<BuildOptions> buildOptions, IOptions<UpdateOptions> updateOptions) : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var output = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory).Path;
        var required = updateOptions.Value.RequireSignature;

        if (required) VerifyNewer(versioning);

        var signature = new ReleaseSignature(ShippedPublicKey());
        if (required && !signature.IsRequired)
            throw new InvalidOperationException("Update:PublicKey in the Daemon appsettings.json is empty. Clients could not verify this release.");

        var manifestPath = Path.Combine(output, UpdateManifest.FileName);
        var manifest = UpdateManifest.FromJson(await VerifySignedAsync(manifestPath, signature, required, cancellationToken));
        Check(manifest.Version == versioning.Version, $"The manifest version {manifest.Version} is not {versioning.Version}.");
        foreach (var payload in manifest.Payloads)
            await VerifyFileAsync(Path.Combine(output, payload.Asset), payload.Sha256, payload.Size, cancellationToken);

        var feeds = Directory.GetFiles(output, "releases.*.json");
        Check(feeds.Length > 0, "No Velopack feed is in the output.");
        foreach (var feedPath in feeds)
        {
            var feed = VelopackAssetFeed.FromJson(System.Text.Encoding.UTF8.GetString(await VerifySignedAsync(feedPath, signature, required, cancellationToken)));
            foreach (var asset in feed.Assets.Where(asset => asset.Version.ToString() == versioning.Version))
                await VerifyFileAsync(Path.Combine(output, asset.FileName), asset.SHA256, asset.Size, cancellationToken);
        }

        context.Summary.KeyValue("Release", "Verified", $"{manifest.Payloads.Count} payloads, {feeds.Length} feed(s), signed: {signature.IsRequired}");
        return versioning.Version;
    }

    private static string? ShippedPublicKey() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(Path.GetDirectoryName(Projects.DevTools_Daemon.FullName)!, "appsettings.json"))
        .Build()
        .GetSection(UpdateEngineOptions.SectionName)
        .Get<UpdateEngineOptions>()?
        .PublicKey;

    private static void VerifyNewer(ResolveVersioningResult versioning)
    {
        if (!SemanticVersion.TryParse(versioning.PreviousVersion, out var previous)) return;

        Check(SemanticVersion.Parse(versioning.Version).CompareTo(previous) > 0,
            $"The version {versioning.Version} is not newer than the previous release {versioning.PreviousVersion}.");
    }

    /// <summary>
    ///     A release must have a correct signature. A CI build has no signing key, so it checks a signature only if one exists.
    /// </summary>
    private static async Task<byte[]> VerifySignedAsync(string path, ReleaseSignature signature, bool required, CancellationToken cancellationToken)
    {
        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        var sigPath = path + ReleaseSignature.Extension;
        if (required) Check(File.Exists(sigPath), $"{Path.GetFileName(sigPath)} is missing.");

        if (signature.IsRequired && File.Exists(sigPath))
            signature.Verify(Path.GetFileName(path), data, await File.ReadAllBytesAsync(sigPath, cancellationToken));

        return data;
    }

    private static async Task VerifyFileAsync(string path, string sha256, long size, CancellationToken cancellationToken)
    {
        Check(File.Exists(path), $"{Path.GetFileName(path)} is missing.");

        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        Check(string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase) && stream.Length == size,
            $"{Path.GetFileName(path)} does not match its feed entry.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
