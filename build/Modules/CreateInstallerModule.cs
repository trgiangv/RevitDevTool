using Build.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.FileSystem;
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
[DependsOn<CompileProjectModule>]
[DependsOn<PublishMcpServerModule>(Optional = true)]
[UsedImplicitly]
public sealed class CreateInstallerModule(IOptions<BuildOptions> buildOptions) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var mcpServerResult = await context.GetModule<PublishMcpServerModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var mcpServerOutputPath = mcpServerResult.ValueOrDefault;

        var wixTarget = new File(Projects.RevitDevTool.FullName);
        var wixInstaller = new File(Projects.Installer.FullName);
        var wixToolFolder = await InstallWixAsync(context, cancellationToken);

        await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = wixInstaller.Path,
            Configuration = "Release"
        }, cancellationToken: cancellationToken);

        var builderFile = wixInstaller.Folder!.GetFolder("bin").FindFile(file => file.NameWithoutExtension == wixInstaller.NameWithoutExtension && file.Extension == ".exe");

        builderFile.ShouldNotBeNull($"No installer builder was found for the project: {wixInstaller.NameWithoutExtension}");

        var targetDirectories = wixTarget.Folder!.GetFolder("bin").GetFolders(folder => folder.Name == "publish").Select(folder => folder.Path).ToArray();

        targetDirectories.ShouldNotBeEmpty("No content were found to create an installer");

        var installerArgs = string.IsNullOrEmpty(mcpServerOutputPath)
            ? (string[])[versioning.Version, ..targetDirectories]
            : (string[])[versioning.Version, ..targetDirectories, mcpServerOutputPath];

        await context.Shell.Command.ExecuteCommandLineTool(new GenericCommandLineToolOptions(builderFile.Path)
        {
            Arguments = installerArgs
        }, new CommandExecutionOptions
        {
            WorkingDirectory = context.Git().RootDirectory,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                { "PATH", $"{Environment.GetEnvironmentVariable("PATH")};{wixToolFolder}" }
            }
        }, cancellationToken: cancellationToken);

        var outputFolder = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        foreach (var outputFile in outputFolder.GetFiles(file => file.Extension == ".msi"))
        {
            context.Summary.KeyValue("Artifacts", "Installer", outputFile.Path);
        }
    }

    /// <summary>
    ///     Installs the WiX toolset required for building installers.
    /// </summary>
    private static async Task<Folder> InstallWixAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var wixToolFolder = Folder.CreateTemporaryFolder();
        await context.DotNet().Tool.Execute(new DotNetToolOptions
        {
            Arguments = ["install", "wix", "--tool-path", wixToolFolder.Path]
        }, cancellationToken: cancellationToken);

        return wixToolFolder;
    }
}