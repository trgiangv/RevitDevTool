using Build.Pipeline;
using DevTools.Update.Autodesk;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;
using System.IO.Compression;

namespace Build.Modules;

/// <summary>
///     Creates one Autodesk .bundle per host from <see cref="AddinHosts"/>: each <c>Contents\&lt;host version&gt;</c> holds the shared
///     files and the files of that host, and <c>PackageContents.xml</c> is the bundle template at the version they were compiled at.
///     The zip has the bundle folder at its root, as <c>--source</c> reads it.
/// </summary>
[DependsOn<CompileExpandModule>(Optional = true)]
[DependsOn<TestProductModule>(Optional = true)]
[UsedImplicitly]
public sealed class CreateBundleModule(BuildRun run) : Module<string[]>
{
    protected override async Task<string[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var hostVersions = await ResolveConfigsModule.HostVersionsAsync(cancellationToken);
        var hostFolders = AddinHosts.Require(run.Root, hostVersions, run.Version.Version);
        var output = Directory.CreateDirectory(run.Output).FullName;

        var bundles = new List<string>();
        foreach (var name in PackageContents.BundleNames)
        {
            var bundle = new AutodeskBundle(Path.Combine(output, name));
            if (Directory.Exists(bundle.Root)) Directory.Delete(bundle.Root, recursive: true);

            foreach (var (hostVersion, hostFolder) in hostVersions.Zip(hostFolders))
            {
                var own = Path.Combine(hostFolder, name);
                if (!Directory.Exists(own))
                    throw new InvalidOperationException($"{hostFolder} has no {name} files; run 'compile addin' again.");

                CopyFolder(Path.Combine(hostFolder, AddinHosts.Shared), bundle.PayloadPath(hostVersion));
                CopyFolder(own, bundle.PayloadPath(hostVersion));
            }

            var manifest = PackageContents.LoadTemplate(name);
            manifest.Root!.SetAttributeValue("AppVersion", run.Version.Version);
            manifest.Save(bundle.PackageContentsPath);

            var zipPath = bundle.Root + ".zip";
            if (File.Exists(zipPath)) File.Delete(zipPath);
            await Task.Run(() => ZipFile.CreateFromDirectory(bundle.Root, zipPath, CompressionLevel.Optimal, includeBaseDirectory: true), cancellationToken);

            context.Summary.KeyValue("Artifacts", name, zipPath);
            bundles.Add(bundle.Root);
        }

        return bundles.ToArray();
    }

    private static void CopyFolder(string source, string target)
    {
        if (!Directory.Exists(source)) return;

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}
