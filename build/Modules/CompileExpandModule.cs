using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Compile every release configuration after <see cref="CompileBaseModule"/>.
/// </summary>
[DependsOn<CompileBaseModule>]
[DependsOn<ResolveVersionModule>]
[DependsOn<ResolveConfigsModule>]
[UsedImplicitly]
public sealed class CompileExpandModule : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var configurationsResult = await context.GetModule<ResolveConfigsModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var configurations = configurationsResult.ValueOrDefault!;

        // Index 0 is CompileBaseModule. Later years stay sequential
        // because they still share bin\Release for mapped projects.
        foreach (var configuration in configurations.Skip(1))
        {
            await context.SubModule(configuration, async () => await SolutionCompile.RunAsync(context, versioning, configuration, cancellationToken));
        }
    }
}
