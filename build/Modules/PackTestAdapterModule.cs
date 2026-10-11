using System.Xml.Linq;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Pack RevitDevTool.TestAdapter into output/nuget.
///     Version comes from DevTools.TestAdapter.csproj. Does not push to nuget.org,
///     and does not wait on host compile, GitVersion, Daemon, TestRunner, or the installer.
/// </summary>
[UsedImplicitly]
public sealed class PackTestAdapterModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var adapterProject = Projects.DevTools_TestAdapter.FullName;
        var mtpProjects = new[]
        {
            Projects.DevTools_NUnit_MTP.FullName,
            Projects.DevTools_TUnit_MTP.FullName,
            Projects.DevTools_MSTest_MTP.FullName
        };

        var version = ReadPackageVersion(adapterProject);
        var outputDir = Path.Combine(context.Git().RootDirectory.Path, "output", "nuget");
        Directory.CreateDirectory(outputDir);

        await RestoreAsync(context, adapterProject, cancellationToken);
        foreach (var project in mtpProjects)
        {
            await RestoreAsync(context, project, cancellationToken);
        }

        foreach (var project in mtpProjects)
        {
            await context.DotNet().Build(new DotNetBuildOptions
            {
                ProjectSolution = project,
                Configuration = "Release",
                NoRestore = true
            }, cancellationToken: cancellationToken);
        }

        await context.DotNet().Pack(new DotNetPackOptions
        {
            ProjectSolution = adapterProject,
            Configuration = "Release",
            NoRestore = true,
            Output = outputDir
        }, cancellationToken: cancellationToken);

        var package = Path.Combine(outputDir, $"RevitDevTool.TestAdapter.{version}.nupkg");
        if (!File.Exists(package))
        {
            throw new InvalidOperationException($"Expected '{package}'.");
        }

        RemoveLastExtraction(version);
        return package;
    }

    private static Task<ModularPipelines.Models.CommandResult> RestoreAsync(IModuleContext context, string project, CancellationToken cancellationToken)
    {
        return context.DotNet().Restore(new DotNetRestoreOptions
        {
            ProjectSolution = project
        }, cancellationToken: cancellationToken);
    }

    private static string ReadPackageVersion(string projectPath)
    {
        var version = XDocument.Load(projectPath)
            .Descendants("Version")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0);

        return string.IsNullOrWhiteSpace(version) ? throw new InvalidOperationException($"Could not read Version from {projectPath}.") : version;
    }

    private static void RemoveLastExtraction(string version)
    {
        var roots = new List<string>();
        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(nugetPackages))
        {
            roots.Add(nugetPackages);
        }

        roots.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages"));

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var cached = Path.Combine(root, "revitdevtool.testadapter", version);
            if (Directory.Exists(cached))
            {
                Directory.Delete(cached, recursive: true);
            }
        }
    }
}
