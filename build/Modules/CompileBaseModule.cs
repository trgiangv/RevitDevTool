using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Builds the first release configuration. Shared projects are mapped to
///     <c>Release</c>, so this produces <c>bin/Release/&lt;tfm&gt;</c> once.
/// </summary>
[DependsOn<ResolveConfigsModule>]
[UsedImplicitly]
public sealed class CompileBaseModule(BuildRun run) : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var configurationsResult = await context.GetModule<ResolveConfigsModule>();
        var configuration = configurationsResult.ValueOrDefault![0];

        await context.SubModule(configuration, async () => await SolutionCompile.RunAsync(context, run, configuration, cancellationToken));
        return configuration;
    }
}
