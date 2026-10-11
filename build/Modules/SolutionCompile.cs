using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using Sourcy.DotNet;

namespace Build.Modules;

internal static class SolutionCompile
{
    public static Task RunAsync(
        IModuleContext context,
        ResolveVersioningResult versioning,
        string configuration,
        CancellationToken cancellationToken)
    {
        return context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = Solutions.RevitDevTool.FullName,
            Configuration = configuration,
            Properties =
            [
                ("VersionPrefix", versioning.VersionPrefix),
                ("VersionSuffix", versioning.VersionSuffix!)
            ]
        }, cancellationToken: cancellationToken);
    }
}
