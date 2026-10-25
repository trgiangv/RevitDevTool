using Build.Options;
using DevTools.Update.Releases;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Writes <c>update-manifest.json</c> with the <see cref="UpdateManifest"/> model of the update engine.
///     Clients read it, not the asset names.
/// </summary>
/// <seealso href="../../docs/decisions/0046-per-year-release-payloads-and-update-manifest.md">0046</seealso>
[DependsOn<ResolveVersionModule>]
[DependsOn<PackPayloadsModule>]
[UsedImplicitly]
public sealed class GenerateUpdateManifestModule(
    IOptions<BuildOptions> buildOptions,
    IOptions<BundleOptions> bundleOptions,
    IOptions<UpdateOptions> updateOptions) : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var payloadsResult = await context.GetModule<PackPayloadsModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var update = updateOptions.Value;

        var manifest = new UpdateManifest
        {
            Schema = UpdateManifest.SupportedSchema,
            Version = versioning.Version,
            Channel = versioning.IsPrerelease ? "prerelease" : "stable",
            PublishedAt = DateTimeOffset.UtcNow,
            NotesUrl = $"{bundleOptions.Value.VendorUrl?.TrimEnd('/')}/releases/tag/{versioning.Version}",
            MinUpdaterVersion = string.IsNullOrWhiteSpace(update.MinUpdaterVersion) ? versioning.Version : update.MinUpdaterVersion,
            IpcCompat = update.IpcCompat,
            Payloads = payloadsResult.ValueOrDefault!
        };

        var manifestPath = Path.Combine(context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory).Path, UpdateManifest.FileName);
        await File.WriteAllBytesAsync(manifestPath, manifest.ToJson(), cancellationToken);

        context.Summary.KeyValue("Artifacts", "Update manifest", manifestPath);
        return manifestPath;
    }
}
