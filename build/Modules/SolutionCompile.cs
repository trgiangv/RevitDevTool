using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Models;
using Sourcy.DotNet;

namespace Build.Modules;

internal static class SolutionCompile
{
    /// <summary>
    /// <c>pack</c> sets this false. The bundle zip is built from
    /// <c>bin/**/publish</c>, so that command keeps <c>PublishAddin</c> and
    /// skips the AppData copy. Both hosts otherwise deploy into the same bundle
    /// and <c>RemoveDir</c> races the other project's copy (<c>MSB3231</c>).
    /// </summary>
    public static bool DeployAddin { get; set; } = true;

    public static Task RunAsync(
        IModuleContext context,
        ResolveVersioningResult versioning,
        string configuration,
        CancellationToken cancellationToken)
    {
        var properties = new List<KeyValue>
        {
            new("VersionPrefix", versioning.VersionPrefix),
            new("VersionSuffix", versioning.VersionSuffix!)
        };

        if (!DeployAddin)
        {
            properties.Add(new KeyValue("DeployAddin", "false"));
            properties.Add(new KeyValue("PublishAddin", "true"));
        }

        return context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = Solutions.RevitDevTool.FullName,
            Configuration = configuration,
            Properties = properties
        }, cancellationToken: cancellationToken);
    }
}
