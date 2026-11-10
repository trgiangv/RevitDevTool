using Build.Pipeline;
using DevTools.Update.Autodesk;
using DevTools.Update.Releases;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;
using Shouldly;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Build.Modules;

/// <summary>
///     Zips each payload folder <c>&lt;bundle&gt;\Contents\&lt;host version&gt;</c> into <see cref="ManifestPayload.AssetName"/>:
///     one zip per template component that has its module file in the bundle.
/// </summary>
[DependsOn<CreateBundleModule>]
[UsedImplicitly]
public sealed class PackPayloadsModule(BuildRun run) : Module<ManifestPayload[]>
{
    protected override async Task<ManifestPayload[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var bundleResult = await context.GetModule<CreateBundleModule>();
        var bundles = bundleResult.ValueOrDefault!.Select(root => new AutodeskBundle(root)).ToDictionary(bundle => bundle.Name);
        var version = run.Version.Version;

        foreach (var name in bundles.Keys)
        {
            foreach (var stale in Directory.GetFiles(run.Output, $"{Path.GetFileNameWithoutExtension(name)}-*-*.zip")) File.Delete(stale);
        }

        var components = PackageContents.TemplateComponents()
            .Where(component => File.Exists(Path.Combine(bundles[component.Bundle].Root, component.ModulePath)))
            .OrderBy(component => component.Bundle, StringComparer.Ordinal)
            .ThenBy(component => component.Target.HostVersion, StringComparer.Ordinal)
            .ToArray();
        components.ShouldNotBeEmpty("No template component has its module file in a bundle");

        var payloads = new List<ManifestPayload>();
        foreach (var component in components)
        {
            var asset = ManifestPayload.AssetName(component.Target, version);
            var zipPath = Path.Combine(run.Output, asset);
            var folder = bundles[component.Bundle].PayloadPath(component.Target.HostVersion);
            await Task.Run(() => ZipFile.CreateFromDirectory(folder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false), cancellationToken);

            await using var zip = File.OpenRead(zipPath);
            payloads.Add(new ManifestPayload
            {
                Platform = component.Target.Platform,
                HostVersion = component.Target.HostVersion,
                Asset = asset,
                Sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(zip, cancellationToken)),
                Size = zip.Length
            });
            context.Summary.KeyValue("Artifacts", component.Description, asset);
        }

        return payloads.ToArray();
    }
}
