using Build.Pipeline;
using Build.Tools;
using Build.Trains;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Builds and runs every test project of the train. A test project that reaches a host project
///     builds with the newest <c>Debug.Autodesk</c> configuration on one node (shared libraries of a year
///     configuration share one <c>obj</c> folder across target frameworks), the others with <c>Debug</c>,
///     because the tests read developer diagnostics that Release compiles out.
/// </summary>
[DependsOn<CompileTrainModule>(Optional = true)]
[DependsOn<CompileExpandModule>(Optional = true)]
[UsedImplicitly]
public sealed class TestTrainModule(BuildRun run) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var hostYear = (await ResolveConfigsModule.ReleaseConfigurationsAsync(cancellationToken)).Last().Replace("Release", "Debug", StringComparison.Ordinal);
        var failed = new List<string>();
        foreach (var project in TrainPaths.TestProjects(run.Root, run.Train))
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var needsHostYear = TrainPaths.NeedsHostYear(run.Root, project);
            var configuration = needsHostYear ? hostYear : "Debug";
            await context.SubModule(name, async () =>
            {
                string[] build = ["build", project, "-c", configuration, "-p:DeployAddin=false"];
                await ModuleShell.DotNetAsync(context, run.Root, needsHostYear ? [.. build, "-m:1"] : build, cancellationToken);
                foreach (var framework in TrainPaths.TargetFrameworks(run.Root, project))
                {
                    var result = await ModuleShell.DotNetAsync(context, run.Root, ["run", "--project", project, "-c", configuration, "-f", framework, "--no-build"], cancellationToken, throwOnError: false);
                    if (result.ExitCode != 0) failed.Add($"{name} ({framework})");
                }
            });
        }

        if (failed.Count > 0)
        {
            throw new InvalidOperationException($"Tests failed in {string.Join(", ", failed)}; run 'dotnet run --project tests/<project>' to reproduce.");
        }
    }
}
