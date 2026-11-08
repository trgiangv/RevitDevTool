using Build.Options;
using Build.Trains;
using DevTools.Update.Releases;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Writes <c>update-manifest.json</c> with the <see cref="UpdateManifest"/> model of the update engine.
///     Clients read it, not the asset names.
/// </summary>
/// <seealso href="../../docs/decisions/0046-per-year-release-payloads-and-update-manifest.md">0046</seealso>
[DependsOn<PackPayloadsModule>]
[UsedImplicitly]
public sealed class GenerateUpdateManifestModule(
    BuildRun run,
    IOptions<BundleOptions> bundleOptions,
    IOptions<UpdateOptions> updateOptions) : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var payloadsResult = await context.GetModule<PackPayloadsModule>();
        var version = run.Version;

        var manifest = new UpdateManifest
        {
            Schema = UpdateManifest.SupportedSchema,
            Version = version.Version,
            Channel = version.IsPrerelease ? "prerelease" : "stable",
            PublishedAt = DateTimeOffset.UtcNow,
            NotesUrl = $"{bundleOptions.Value.VendorUrl?.TrimEnd('/')}/releases/tag/{version.Tag}",
            MinUpdaterVersion = VersionFile.ReadProperty(run.Root, run.Train.VersionFile, ReleaseRules.MinUpdaterProperty),
            IpcCompat = updateOptions.Value.IpcCompat,
            Payloads = payloadsResult.ValueOrDefault!
        };

        var manifestPath = Path.Combine(run.Output, UpdateManifest.FileName);
        await File.WriteAllBytesAsync(manifestPath, manifest.ToJson(), cancellationToken);

        context.Summary.KeyValue("Artifacts", "Update manifest", manifestPath);
        return manifestPath;
    }
}
