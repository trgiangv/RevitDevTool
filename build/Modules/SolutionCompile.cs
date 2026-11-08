using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Models;
using Sourcy.DotNet;

namespace Build.Modules;

internal static class SolutionCompile
{
    /// <summary>
    ///     The bundle zip is built from <c>bin/**/publish</c>, so an artifact build keeps <c>PublishAddin</c>.
    ///     Both hosts would otherwise deploy into the same bundle and <c>RemoveDir</c> races the other
    ///     project's copy (<c>MSB3231</c>). <c>TrainVersion</c> is read only by <c>source/AddinVersion.props</c>.
    /// </summary>
    public static Task RunAsync(IModuleContext context, BuildRun run, string configuration, CancellationToken cancellationToken)
    {
        var properties = new List<KeyValue> { new("DeployAddin", "false") };
        if (run.BuildsArtifacts)
        {
            properties.Add(new KeyValue("PublishAddin", "true"));
            properties.Add(new KeyValue("TrainVersion", run.Version.Version));
        }

        return context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = Solutions.RevitDevTool.FullName,
            Configuration = configuration,
            Properties = properties
        }, cancellationToken: cancellationToken);
    }
}
