using Build.Pipeline;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Compile every release configuration after <see cref="CompileBaseModule"/>.
/// </summary>
[DependsOn<CompileBaseModule>]
[DependsOn<ResolveConfigsModule>]
[UsedImplicitly]
public sealed class CompileExpandModule(BuildRun run) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var configurationsResult = await context.GetModule<ResolveConfigsModule>();
        var configurations = configurationsResult.ValueOrDefault!;

        // Index 0 is CompileBaseModule. Later years stay sequential
        // because they still share bin\Release for mapped projects.
        foreach (var configuration in configurations.Skip(1))
        {
            await context.SubModule(configuration, async () => await SolutionCompile.RunAsync(context, run, configuration, cancellationToken));
        }
    }
}
