using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Builds the first release configuration. Shared projects are mapped to
///     <c>Release</c>, so this produces <c>bin/Release/&lt;tfm&gt;</c> once.
///     Daemon and TestRunner publish after this module, while
///     <see cref="CompileExpandModule"/> is still compiling later years.
/// </summary>
[DependsOn<ResolveVersionModule>]
[DependsOn<ResolveConfigsModule>]
[UsedImplicitly]
public sealed class CompileBaseModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var configurationsResult = await context.GetModule<ResolveConfigsModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var configuration = configurationsResult.ValueOrDefault![0];

        await context.SubModule(configuration, async () => await SolutionCompile.RunAsync(context, versioning, configuration, cancellationToken));
        return configuration;
    }
}
