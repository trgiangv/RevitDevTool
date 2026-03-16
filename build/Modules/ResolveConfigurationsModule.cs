using JetBrains.Annotations;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using ModularPipelines.Context;
using ModularPipelines.Modules;
using Shouldly;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Resolve solution configurations required to compile the add-in for all supported Revit versions.
/// </summary>
[UsedImplicitly]
public sealed class ResolveConfigurationsModule : Module<string[]>
{
    protected override async Task<string[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var solutionModel = await LoadSolutionModelAsync(cancellationToken);
        var configurations = solutionModel.BuildTypes.Where(configuration => configuration.Contains("Release.Autodesk", StringComparison.OrdinalIgnoreCase)).ToArray();

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