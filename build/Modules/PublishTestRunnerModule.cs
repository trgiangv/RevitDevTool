using DevTools.Utilities;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Publish DevTools.TestRunner as a self-contained single-file executable.
///     The csproj DeployDevToolsTestRunner target copies it beside DevTools.Daemon.exe.
/// </summary>
[DependsOn<ResolveVersioningModule>]
[DependsOn<CompileProjectModule>]
[DependsOn<CleanProjectModule>(Optional = true)]
[UsedImplicitly]
public sealed class PublishTestRunnerModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var versioning = versioningResult.ValueOrDefault!;

        await context.DotNet().Publish(new DotNetPublishOptions
        {
            ProjectSolution = Projects.DevTools_TestRunner.FullName,
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
