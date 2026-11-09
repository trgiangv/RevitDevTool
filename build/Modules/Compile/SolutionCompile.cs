using Build.Pipeline;
using Build.Tools;
using ModularPipelines.Context;
using Sourcy.DotNet;

namespace Build.Modules;

internal static class SolutionCompile
{
    /// <summary>
    ///     Builds only the host projects and their references, through the solution so its configuration mapping applies.
    ///     Every compile publishes into <c>bin/**/publish</c> with the run version, so <c>pack addin</c> can bundle it later
    ///     or on another runner. <c>DeployAddin=false</c>: both hosts deploying into one bundle race <c>RemoveDir</c> (<c>MSB3231</c>).
    /// </summary>
    public static Task RunAsync(IModuleContext context, BuildRun run, string configuration, CancellationToken cancellationToken)
    {
        var projects = string.Join(';', run.Product.Roots.Select(Path.GetFileNameWithoutExtension));
        return ModuleShell.DotNetAsync(context, run.Root,
        [
            "build", Solutions.RevitDevTool.FullName, "-c", configuration, $"-t:{projects}",
            "-p:DeployAddin=false", "-p:PublishAddin=true", $"-p:AddinVersion={run.Version.Version}"
        ], cancellationToken);
    }
}
