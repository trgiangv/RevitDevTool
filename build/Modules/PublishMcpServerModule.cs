using Build.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Publish the MCP server as a self-contained single-file executable.
/// </summary>
[DependsOn<ResolveVersioningModule>]
[DependsOn<CleanProjectModule>(Optional = true)]
[UsedImplicitly]
public sealed class PublishMcpServerModule(IOptions<BuildOptions> buildOptions) : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var versioning = versioningResult.ValueOrDefault!;

        var outputFolder = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        var mcpServerOutputPath = outputFolder.GetFolder("MCPServer").Path;

        await context.DotNet().Publish(new DotNetPublishOptions
        {
            ProjectSolution = Projects.RevitDevTool_McpServer.FullName,
            Configuration = "Release",
            Properties =
            [
                ("VersionPrefix", versioning.VersionPrefix),
                ("VersionSuffix", versioning.VersionSuffix!),
                ("PublishDir", mcpServerOutputPath)
            ]
        }, cancellationToken: cancellationToken);

        context.Summary.KeyValue("Artifacts", "MCPServer", mcpServerOutputPath);

        return mcpServerOutputPath;
    }
}
