using Build.Pipeline;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using ModularPipelines.Context;
using ModularPipelines.Modules;
using Shouldly;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     The <c>Release.Autodesk.&lt;host version&gt;</c> solution configurations the add-in compiles, or the one host version of the run.
/// </summary>
[UsedImplicitly]
public sealed class ResolveConfigsModule(BuildRun run) : Module<string[]>
{
    protected override async Task<string[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken) =>
        (await ReleaseConfigurationsAsync(cancellationToken))
        .Where(configuration => run.HostVersion is null || configuration.EndsWith($".{run.HostVersion}", StringComparison.Ordinal))
        .ToArray();

    public static async Task<string[]> ReleaseConfigurationsAsync(CancellationToken cancellationToken)
    {
        var solutionModel = await LoadSolutionModelAsync(cancellationToken);
        var configurations = solutionModel.BuildTypes
            .Where(c => c.Contains("Release.Autodesk", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        configurations.ShouldNotBeEmpty("No solution configurations have been found");

        return configurations;
    }

    private static async Task<SolutionModel> LoadSolutionModelAsync(CancellationToken cancellationToken)
    {
        var sampleSolutionPath = Solutions.RevitDevTool.FullName;
        await using var stream = File.OpenRead(sampleSolutionPath);
        return await SolutionSerializers.SlnXml.OpenAsync(stream, cancellationToken);
    }
}
