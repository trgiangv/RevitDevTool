using Build.Pipeline;
using Build.Tools;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Runs the pixi <c>lint</c> and <c>test</c> tasks of the pytest package.
/// </summary>
[UsedImplicitly]
public sealed class TestPyTestModule(BuildRun run) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        await ModuleShell.PixiAsync(context, run.Root, ["lint"], cancellationToken);
        await ModuleShell.PixiAsync(context, run.Root, ["test"], cancellationToken);
    }
}
