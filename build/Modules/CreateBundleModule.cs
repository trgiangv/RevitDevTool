using Build.Options;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.FileSystem;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Shouldly;
using Sourcy.DotNet;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using File = ModularPipelines.FileSystem.File;

namespace Build.Modules;

/// <summary>
///     Create the Autodesk .bundle package: the payload folders and <c>PackageContents.xml</c>.
/// </summary>
[DependsOn<ResolveVersionModule>]
[DependsOn<CompileExpandModule>]
[UsedImplicitly]
public sealed partial class CreateBundleModule(IOptions<BuildOptions> buildOptions) : Module<string>
{
    private const string AdskBundleContentsFolder = "Contents";
    private const string AdskAppVersionAttribute = "AppVersion";
    private const string ManifestFile = "PackageContents.xml";
    private const string BinFolder = "bin";
    private const string PublishFolder = "publish";

    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var versioning = versioningResult.ValueOrDefault!;

        var revitTarget = new File(Projects.RevitDevTool.FullName);
        var acadTarget = new File(Projects.ACadDevTool.FullName);

        var revitPublishDirs = revitTarget.Folder!.GetFolder(BinFolder).GetFolders(IsReleasePublishFolder).ToArray();
        var acadPublishDirs = acadTarget.Folder!.GetFolder(BinFolder).GetFolders(IsReleasePublishFolder).ToArray();

        revitPublishDirs.ShouldNotBeEmpty("No Revit content were found to create a bundle");

        var outputFolder = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        var staleBundle = Path.Combine(outputFolder.Path, $"{revitTarget.NameWithoutExtension}.bundle");
        if (Directory.Exists(staleBundle))
        {
            Directory.Delete(staleBundle, recursive: true);
        }

        var bundleFolder = outputFolder.CreateFolder($"{revitTarget.NameWithoutExtension}.bundle");
        var contentFolder = bundleFolder.CreateFolder(AdskBundleContentsFolder);
        var manifestFile = bundleFolder.GetFile(ManifestFile);

        PackFiles(revitPublishDirs, contentFolder);
        PackFiles(acadPublishDirs, contentFolder);
        CopyManifest(context, manifestFile, versioning);

        var outputFile = outputFolder.GetFile($"{bundleFolder.Name}.zip");
        if (outputFile.Exists)
        {
            await outputFile.DeleteAsync(cancellationToken);
        }

        context.Files.Zip.ZipFolder(bundleFolder, outputFile.Path);

        context.Summary.KeyValue("Artifacts", "Bundle", outputFile.Path);
        return bundleFolder.Path;
    }

    /// <summary>
    ///     Only <c>bin/Release.*/publish</c>. A local <c>Debug.*</c> publish must not leak into the bundle.
    /// </summary>
    private static bool IsReleasePublishFolder(Folder folder)
    {
        return folder.Name == PublishFolder &&
               Path.GetFileName(Path.GetDirectoryName(folder.Path))!.StartsWith("Release", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Copy each host's output into <c>Contents\&lt;host version&gt;</c>. Hosts of one host version share the folder,
    ///     so a file both hosts publish must be byte-identical (0046).
    /// </summary>
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

                if (destinationPath.Exists && !HaveSameContent(filePath.Path, destinationPath.Path))
                {
                    throw new InvalidOperationException(
                        $"Contents\\{version}\\{relativePath} differs between hosts of the same host version: '{filePath.Path}' vs '{destinationPath.Path}'.");
                }

                filePath.CopyTo(destinationPath.Path);
            }
        }
    }

    private static bool HaveSameContent(string left, string right)
    {
        if (new FileInfo(left).Length != new FileInfo(right).Length) return false;

        using var leftStream = System.IO.File.OpenRead(left);
        using var rightStream = System.IO.File.OpenRead(right);
        return SHA256.HashData(leftStream).AsSpan().SequenceEqual(SHA256.HashData(rightStream));
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
