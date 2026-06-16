using DevTools.Utilities;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Publish DevTools.Daemon as a self-contained single-file executable.
///     The csproj DeployDevToolsDaemon target handles kill + copy to bundle Contents.
/// </summary>
[DependsOn<ResolveVersioningModule>]
[DependsOn<CleanProjectModule>(Optional = true)]
[UsedImplicitly]
public sealed class PublishDaemonModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var versioning = versioningResult.ValueOrDefault!;

        await context.DotNet().Publish(new DotNetPublishOptions
        {
            ProjectSolution = Projects.DevTools_Daemon.FullName,
            Configuration = "Release",
            Properties =
            [
                ("VersionPrefix", versioning.VersionPrefix),
                ("VersionSuffix", versioning.VersionSuffix!)
            ]
        }, cancellationToken: cancellationToken);

        return AppUtils.GetBundleContentsPath();
    }
}
