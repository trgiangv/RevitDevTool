using Build.Options;
using DevTools.Update.Autodesk;
using DevTools.Update.Releases;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Shouldly;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Build.Modules;

/// <summary>
///     Zips each payload folder <c>Contents\&lt;host version&gt;</c> into <c>RevitDevTool-&lt;version&gt;-&lt;host version&gt;.zip</c>.
///     The platforms of a payload are the template components that have their module file in the bundle.
/// </summary>
[DependsOn<ResolveVersionModule>]
[DependsOn<CreateBundleModule>]
[UsedImplicitly]
public sealed class PackPayloadsModule(IOptions<BuildOptions> buildOptions) : Module<ManifestPayload[]>
{
    protected override async Task<ManifestPayload[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var bundleResult = await context.GetModule<CreateBundleModule>();
        var version = versioningResult.ValueOrDefault!.Version;
        var bundle = new AutodeskBundle(bundleResult.ValueOrDefault!);
        var outputPath = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory).Path;

        foreach (var stale in Directory.GetFiles(outputPath, "RevitDevTool-*-*.zip")) File.Delete(stale);

        var platformsByHostVersion = PackageContents.TemplateComponents()
            .Where(component => File.Exists(Path.Combine(bundle.Root, component.ModulePath)))
            .GroupBy(component => component.Target.HostVersion, component => component.Target.Platform)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        platformsByHostVersion.ShouldNotBeEmpty("No template component has its module file in the bundle");

        var payloads = new List<ManifestPayload>();
        foreach (var group in platformsByHostVersion)
        {
            var asset = $"RevitDevTool-{version}-{group.Key}.zip";
            var zipPath = Path.Combine(outputPath, asset);
            await Task.Run(() => ZipFile.CreateFromDirectory(bundle.PayloadPath(group.Key), zipPath, CompressionLevel.Optimal, includeBaseDirectory: false), cancellationToken);

            await using var zip = File.OpenRead(zipPath);
            payloads.Add(new ManifestPayload
            {
                HostVersion = group.Key,
                Platforms = group.Order(StringComparer.Ordinal).ToArray(),
                Asset = asset,
                Sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(zip, cancellationToken)),
                Size = zip.Length
            });
            context.Summary.KeyValue("Artifacts", $"Payload {group.Key}", $"{asset} ({string.Join(", ", group)})");
        }

        return payloads.ToArray();
    }
}
