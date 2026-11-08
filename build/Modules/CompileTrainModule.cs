using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

[UsedImplicitly]
public sealed class CompileTrainModule(BuildRun run) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        foreach (var root in run.Train.Roots)
        {
            await context.SubModule(Path.GetFileNameWithoutExtension(root), async () =>
                await DotNetCli.RunAsync(context, run.Root, ["build", root, "-c", "Release"], cancellationToken));
        }
    }
}
