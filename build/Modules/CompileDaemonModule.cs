using DevTools.Utilities;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Publish DevTools.Daemon as a framework-dependent single-file exe (requires .NET 10 runtime).
///     The csproj DeployDevToolsDaemon target handles kill + copy to bundle Contents.
/// </summary>
[DependsOn<ResolveVersionModule>]
[DependsOn<CompileBaseModule>]
[UsedImplicitly]
public sealed class CompileDaemonModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var versioning = versioningResult.ValueOrDefault!;

        await context.DotNet().Publish(new DotNetPublishOptions
        {
            ProjectSolution = Projects.DevTools_Daemon.FullName,
            Configuration = "Release",
            Properties =
            [
                ("VersionPrefix", versioning.VersionPrefix),
                ("VersionSuffix", versioning.VersionSuffix!),
                ("BuildProjectReferences", "false")
            ]
        }, cancellationToken: cancellationToken);

        return AppUtils.GetBundleContentsPath();
    }
}
