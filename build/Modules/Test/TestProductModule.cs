using Build.Pipeline;
using Build.Products;
using Build.Tools;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;
using System.Xml.Linq;

namespace Build.Modules;

/// <summary>
///     Builds and runs the test projects of the product (<see cref="ProjectGraph.TestProjects(string, Product)"/>). The projects
///     build in one MSBuild call per configuration: a test project that reaches a host project uses the newest
///     <c>Debug.Autodesk</c> configuration on one node (shared libraries of a year configuration share one <c>obj</c> folder across
///     target frameworks), the others use <c>Debug</c>, because the tests read developer diagnostics that Release compiles out.
///     An <c>MSTest.Sdk</c> test that runs past <see cref="HangTimeout"/> stops its test host with a hang dump that names the running tests.
/// </summary>
[DependsOn<CompileProductModule>(Optional = true)]
[DependsOn<CompileExpandModule>(Optional = true)]
[UsedImplicitly]
public sealed class TestProductModule(BuildRun run) : Module
{
    private const string HangTimeout = "5m";

    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var hostYear = (await ResolveConfigsModule.ReleaseConfigurationsAsync(cancellationToken)).Last().Replace("Release", "Debug", StringComparison.Ordinal);
        var projects = ProjectGraph.TestProjects(run.Root, run.Product)
            .Select(project => (Project: project, Configuration: ProjectGraph.NeedsHostYear(run.Root, project) ? hostYear : "Debug"))
            .ToArray();

        foreach (var group in projects.GroupBy(item => item.Configuration))
        {
            var traversal = WriteTraversal(group.Key, group.Select(item => item.Project));
            string[] build = ["build", traversal, "-c", group.Key, "-p:DeployAddin=false"];
            await context.SubModule($"build {group.Key}", () =>
                ModuleShell.DotNetAsync(context, run.Root, group.Key == hostYear ? [.. build, "-m:1"] : build, cancellationToken));
        }

        var failed = new List<string>();
        foreach (var (project, configuration) in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            string[] hangDump = ProjectGraph.UsesTestingPlatform(run.Root, project)
                ? ["--", "--hangdump", "--hangdump-timeout", HangTimeout, "--hangdump-type", "Mini"]
                : [];
            await context.SubModule(name, async () =>
            {
                foreach (var framework in ProjectGraph.TargetFrameworks(run.Root, project))
                {
                    var result = await ModuleShell.DotNetAsync(context, run.Root,
                        ["run", "--project", project, "-c", configuration, "-f", framework, "--no-build", .. hangDump], cancellationToken, throwOnError: false);
                    if (result.ExitCode != 0) failed.Add($"{name} ({framework})");
                }
            });
        }

        if (failed.Count > 0)
        {
            throw new InvalidOperationException($"Tests failed in {string.Join(", ", failed)}; run 'dotnet run --project tests/<project>' to reproduce.");
        }
    }

    /// <summary>
    ///     A plain MSBuild project (no SDK, so no <c>Directory.Build.props</c>) whose <c>Restore</c> and <c>Build</c> pass to the test
    ///     projects with the global properties of the call. Restore runs one project at a time, because their closures overlap.
    /// </summary>
    private string WriteTraversal(string configuration, IEnumerable<string> projects)
    {
        var path = Path.Combine(run.Root, "output", "tests", $"{run.Product.Name}.{configuration}.proj");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        new XDocument(new XElement("Project", new XAttribute("DefaultTargets", "Build"),
            new XElement("ItemGroup", projects.Select(project => new XElement("TestProject", new XAttribute("Include", Path.Combine(run.Root, project))))),
            new XElement("Target", new XAttribute("Name", "Restore"),
                new XElement("MSBuild", new XAttribute("Projects", "@(TestProject)"), new XAttribute("Targets", "Restore"))),
            new XElement("Target", new XAttribute("Name", "Build"),
                new XElement("MSBuild", new XAttribute("Projects", "@(TestProject)"), new XAttribute("Targets", "Build"), new XAttribute("BuildInParallel", "true")))))
            .Save(path);
        return path;
    }
}
