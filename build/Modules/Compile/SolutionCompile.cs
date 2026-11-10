using Build.Pipeline;
using Build.Tools;
using ModularPipelines.Context;
using Sourcy.DotNet;

namespace Build.Modules;

internal static class SolutionCompile
{
    /// <summary>
    ///     Builds only the host projects and their references, through the solution so its configuration mapping applies,
    ///     then collects the host version into <see cref="AddinHosts"/>. The host output folders start empty, because the publish
    ///     copies everything in them. <c>DeployAddin=false</c>: a compile does not install into <c>%APPDATA%</c>.
    /// </summary>
    public static async Task RunAsync(IModuleContext context, BuildRun run, string configuration, CancellationToken cancellationToken)
    {
        foreach (var output in run.Product.Roots.Select(project => Path.Combine(run.Root, Path.GetDirectoryName(project)!, "bin", configuration)))
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }

        var projects = string.Join(';', run.Product.Roots.Select(Path.GetFileNameWithoutExtension));
        await ModuleShell.DotNetAsync(context, run.Root,
        [
            "build", Solutions.RevitDevTool.FullName, "-c", configuration, $"-t:{projects}",
            "-p:DeployAddin=false", "-p:PublishAddin=true", $"-p:AddinVersion={run.Version.Version}"
        ], cancellationToken);
        AddinHosts.Collect(run, configuration);
    }
}
