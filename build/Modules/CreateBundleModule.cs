using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Xml;
using Build.Options;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.FileSystem;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Shouldly;
using Sourcy.DotNet;
using File = ModularPipelines.FileSystem.File;

namespace Build.Modules;

/// <summary>
///     Create the Autodesk .bundle package.
/// </summary>
[DependsOn<ResolveVersioningModule>]
[DependsOn<CompileProjectModule>]
[DependsOn<PublishDaemonModule>]
[UsedImplicitly]
public sealed partial class CreateBundleModule(IOptions<BuildOptions> buildOptions) : Module<string>
{
    private const string AdskBundleContentsFolder = "Contents";
    private const string AdskAppVersionAttribute = "AppVersion";
    private const string ManifestFile = "PackageContents.xml";
    private const string BinFolder = "bin";
    private const string PublishFolder = "publish";
    private const string DaemonExecutable = "DevTools.Daemon.exe";
    
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var daemonResult = await context.GetModule<PublishDaemonModule>();
        var versioning = versioningResult.ValueOrDefault!;

        var revitTarget = new File(Projects.RevitDevTool.FullName);
        var acadTarget = new File(Path.Combine(revitTarget.Folder!.Parent!.Path, "AcadDevTool", "AcadDevTool.csproj"));

        var revitPublishDirs = revitTarget.Folder!.GetFolder(BinFolder).GetFolders(folder => folder.Name == PublishFolder).ToArray();
        var acadPublishDirs = acadTarget.Folder!.GetFolder(BinFolder).GetFolders(folder => folder.Name == PublishFolder).ToArray();

        revitPublishDirs.ShouldNotBeEmpty("No Revit content were found to create a bundle");

        var outputFolder = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        var bundleFolder = outputFolder.CreateFolder($"{revitTarget.NameWithoutExtension}.bundle");
        var contentFolder = bundleFolder.CreateFolder(AdskBundleContentsFolder);
        var manifestFile = bundleFolder.GetFile(ManifestFile);

        PackFiles(revitPublishDirs, contentFolder);
        PackFiles(acadPublishDirs, contentFolder);
        PackDaemon(daemonResult.ValueOrDefault!, contentFolder);
        CopyManifest(context, manifestFile, versioning);

        var outputFile = outputFolder.GetFile($"{bundleFolder.Name}.zip");
        context.Files.Zip.ZipFolder(bundleFolder, outputFile.Path);

        context.Summary.KeyValue("Artifacts", "Bundle", outputFile.Path);
        return bundleFolder.Path;
    }

    private static void PackFiles(Folder[] targetDirectories, Folder contentFolder)
    {
        foreach (var targetDirectory in targetDirectories)
        {
            TryParseVersion(targetDirectory.Path, out var version).ShouldBeTrue($"Could not parse version from directory name: {targetDirectory.Path}");

            var sourceDir = targetDirectory.GetFolder(AdskBundleContentsFolder).GetFolder(version);
            if (!sourceDir.Exists) continue;

            var versionFolder = contentFolder.CreateFolder(version);
            foreach (var filePath in sourceDir.GetFiles(file => file.Exists))
            {
                var relativePath = Path.GetRelativePath(sourceDir.Path, filePath.Path);
                var destinationPath = versionFolder.GetFile(relativePath);
                if (!destinationPath.Folder!.Exists)
                {
                    destinationPath.Folder!.Create();
                }

                filePath.CopyTo(destinationPath.Path);
            }
        }
    }

    private static void PackDaemon(string daemonPublishDir, Folder contentFolder)
    {
        var daemonExe = new File(Path.Combine(daemonPublishDir, DaemonExecutable));
        if (!daemonExe.Exists) return;

        daemonExe.CopyTo(contentFolder.GetFile(DaemonExecutable).Path);
    }

    private static void CopyManifest(IModuleContext context, File manifestFile, ResolveVersioningResult versioning)
    {
        var propsDir = context.Git().RootDirectory.GetFolder("props");
        var sourceManifest = propsDir.GetFile(ManifestFile);

        var xml = new XmlDocument();
        xml.Load(sourceManifest.Path);

        var root = xml.DocumentElement!;
        root.SetAttribute(AdskAppVersionAttribute, versioning.Version);

        xml.Save(manifestFile.Path);
    }

    /// <summary>
    ///     Parse a version string from the given input.
    /// </summary>
    private static bool TryParseVersion(string input, [NotNullWhen(true)] out string? version)
    {
        version = null;
        var match = VersionRegex().Match(input);
        if (!match.Success) return false;

        switch (match.Value.Length)
        {
            case 4:
                version = match.Value;
                return true;
            case 2:
                version = $"20{match.Value}";
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     A regular expression to match the last sequence of numeric characters in a string.
    /// </summary>
    [GeneratedRegex(@"(\d+)(?!.*\d)")]
    private static partial Regex VersionRegex();
}