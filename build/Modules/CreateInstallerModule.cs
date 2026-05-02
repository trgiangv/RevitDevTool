using Build.Options;
using Microsoft.Extensions.Logging;
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
///     Create the Inno Setup .exe installer.
/// </summary>
/// <remarks>
///     Requires Inno Setup 6 to be installed and ISCC.exe available in PATH.
/// </remarks>
[DependsOn<ResolveVersioningModule>]
[DependsOn<CreateBundleModule>]
[UsedImplicitly]
public sealed class CreateInstallerModule(IOptions<BuildOptions> buildOptions) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var isccPath = await FindIscc(context, cancellationToken);
        if (string.IsNullOrEmpty(isccPath))
        {
            throw new InvalidOperationException("""
                ISCC (Inno Setup Compiler) not found in PATH.
                Please install Inno Setup 6 from: https://jrsoftware.org/isdl.php
                """);
        }

        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var bundleResult = await context.GetModule<CreateBundleModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var bundleFolderPath = bundleResult.ValueOrDefault!;

        var outputDirectory = buildOptions.Value.OutputDirectory;
        var outputFolder = context.Git().RootDirectory.GetFolder(outputDirectory);

        // Build the InnoSetup CLI tool
        var installerProject = new File(Projects.Installer.FullName);

        context.Logger.LogInformation("Building Installer CLI...");
        await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = installerProject.Path,
            Configuration = "Release"
        }, cancellationToken: cancellationToken);

        var builderFile = installerProject.Folder!
            .GetFolder("bin")
            .FindFile(f => f.Name == "Installer.exe");

        builderFile.ShouldNotBeNull($"No Installer.exe found for project: {installerProject.Name}");

        // Call Installer CLI
        // Args: <version> <bundle-path> <output-path>
        var installerArgs = new[]
        {
            versioning.Version,
            bundleFolderPath,
            outputFolder.Path
        };

        var result = await context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions(builderFile.Path)
            {
                Arguments = installerArgs
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = context.Git().RootDirectory,
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["PATH"] = Environment.GetEnvironmentVariable("PATH")
                }
            },
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"InnoSetupBuilder failed with exit code {result.ExitCode}. Check logs above.");
        }

        // Find the generated .exe installer
        var exeFile = outputFolder.FindFile(f => f.Name == "RevitDevTool-Setup.exe");

        if (exeFile == null)
        {
            throw new InvalidOperationException(
                "No RevitDevTool-Setup.exe installer was generated. Check InnoSetupBuilder output for errors.");
        }

        context.Summary.KeyValue("Artifacts", "Installer", exeFile.Path);
    }

    /// <summary>
    /// Find ISCC using where.exe
    /// </summary>
    private static async Task<string?> FindIscc(IModuleContext context, CancellationToken cancellationToken)
    {
        var result = await context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions("where.exe")
            {
                Arguments = ["iscc"]
            },
            cancellationToken: cancellationToken);

        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            var path = result.StandardOutput
                .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }
        }

        context.Logger.LogError("ISCC.exe not found in PATH");
        return null;
    }
}