using Build.Pipeline;
using Build.Tools;
using ModularPipelines.Context;
using Sourcy.DotNet;

namespace Build.Modules;

internal static class SolutionCompile
{
    /// <summary>
    ///     Builds only the host projects and their references, through the solution so its configuration mapping applies,
    ///     then collects the host version into <see cref="AddinHosts"/>. <c>DeployAddin=false</c>: both hosts deploying into one
    ///     bundle race <c>RemoveDir</c> (<c>MSB3231</c>).
    /// </summary>
    public static async Task RunAsync(IModuleContext context, BuildRun run, string configuration, CancellationToken cancellationToken)
    {
        var projects = string.Join(';', run.Product.Roots.Select(Path.GetFileNameWithoutExtension));
        await ModuleShell.DotNetAsync(context, run.Root,
        [
            "build", Solutions.RevitDevTool.FullName, "-c", configuration, $"-t:{projects}",
            "-p:DeployAddin=false", "-p:PublishAddin=true", $"-p:AddinVersion={run.Version.Version}"
        ], cancellationToken);
        AddinHosts.Collect(run, configuration);
    }
}
