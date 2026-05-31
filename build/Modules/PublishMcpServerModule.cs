using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Publish MCPServer as a trimmed self-contained single-file executable.
///     The csproj DeployMcpServer target handles kill + copy to bundle Contents.
/// </summary>
[DependsOn<ResolveVersioningModule>]
[DependsOn<CleanProjectModule>(Optional = true)]
[UsedImplicitly]
public sealed class PublishMcpServerModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var versioning = versioningResult.ValueOrDefault!;

        await context.DotNet().Publish(new DotNetPublishOptions
        {
            ProjectSolution = Projects.DevTools_McpServer.FullName,
            Configuration = "Release",
            Properties =
            [
                ("VersionPrefix", versioning.VersionPrefix),
                ("VersionSuffix", versioning.VersionSuffix!)
            ]
        }, cancellationToken: cancellationToken);

        var bundleContents = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "ApplicationPlugins", "RevitDevTool.bundle", "Contents");

        return bundleContents;
    }
}
