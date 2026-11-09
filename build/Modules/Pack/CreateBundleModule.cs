using Build.Pipeline;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Sourcy.DotNet;
using System.Xml;

namespace Build.Modules;

/// <summary>
///     Create the Autodesk .bundle package from <see cref="AddinHosts"/>: one <c>Contents\&lt;host version&gt;</c> per
///     <c>Release.Autodesk.*</c> configuration, and <c>PackageContents.xml</c> at the version they were compiled at.
/// </summary>
[DependsOn<CompileExpandModule>(Optional = true)]
[DependsOn<TestProductModule>(Optional = true)]
[UsedImplicitly]
public sealed class CreateBundleModule(BuildRun run) : Module<string>
{
    private const string ManifestFile = "PackageContents.xml";

    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var hostVersions = await ResolveConfigsModule.HostVersionsAsync(cancellationToken);
        var hostFolders = AddinHosts.Require(run.Root, hostVersions, run.Version.Version);

        var bundleName = $"{Path.GetFileNameWithoutExtension(Projects.RevitDevTool.FullName)}.bundle";
        var bundleFolder = Path.Combine(Directory.CreateDirectory(run.Output).FullName, bundleName);
        if (Directory.Exists(bundleFolder)) Directory.Delete(bundleFolder, recursive: true);

        foreach (var (hostVersion, hostFolder) in hostVersions.Zip(hostFolders))
        {
            CopyFolder(hostFolder, Path.Combine(bundleFolder, "Contents", hostVersion));
        }

        CopyManifest(context, Path.Combine(bundleFolder, ManifestFile), run.Version.Version);

        var zipPath = bundleFolder + ".zip";
        if (File.Exists(zipPath)) File.Delete(zipPath);
        context.Files.Zip.ZipFolder(new ModularPipelines.FileSystem.Folder(bundleFolder), zipPath);

        context.Summary.KeyValue("Artifacts", "Bundle", zipPath);
        return bundleFolder;
    }

    private static void CopyFolder(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    private static void CopyManifest(IModuleContext context, string manifestPath, string version)
    {
        var xml = new XmlDocument();
        xml.Load(context.Git().RootDirectory.GetFolder("props").GetFile(ManifestFile).Path);
        xml.DocumentElement!.SetAttribute("AppVersion", version);
        xml.Save(manifestPath);
    }
}
