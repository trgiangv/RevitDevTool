using Build.Options;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Shouldly;
using Sourcy.DotNet;
using File = ModularPipelines.FileSystem.File;

namespace Build.Modules;

/// <summary>
///     Create the .msi installer.
/// </summary>
[DependsOn<ResolveVersioningModule>]
[DependsOn<CreateBundleModule>]
[UsedImplicitly]
public sealed class CreateInstallerModule(IOptions<BuildOptions> buildOptions) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var bundleResult = await context.GetModule<CreateBundleModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var bundleFolderPath = bundleResult.ValueOrDefault!;

        var wixInstaller = new File(Projects.Installer.FullName);

        await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = wixInstaller.Path,
            Configuration = "Release"
        }, cancellationToken: cancellationToken);

        var builderFile = wixInstaller.Folder!.GetFolder("bin").FindFile(file => file.NameWithoutExtension == wixInstaller.NameWithoutExtension && file.Extension == ".exe");
        builderFile.ShouldNotBeNull($"No installer builder was found for the project: {wixInstaller.NameWithoutExtension}");

        var installerArgs = (string[])[versioning.Version, bundleFolderPath];

        await context.Shell.Command.ExecuteCommandLineTool(new GenericCommandLineToolOptions(builderFile.Path)
        {
            Arguments = installerArgs
        }, new CommandExecutionOptions
        {
            WorkingDirectory = context.Git().RootDirectory
        }, cancellationToken: cancellationToken);

        var outputFolder = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        foreach (var outputFile in outputFolder.GetFiles(file => file.Extension == ".msi"))
        {
            context.Summary.KeyValue("Artifacts", "Installer", outputFile.Path);
        }
    }

}