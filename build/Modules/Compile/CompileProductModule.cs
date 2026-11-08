using Build.Pipeline;
using Build.Tools;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

[UsedImplicitly]
public sealed class CompileProductModule(BuildRun run) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        foreach (var root in run.Product.Roots)
        {
            await context.SubModule(Path.GetFileNameWithoutExtension(root), async () =>
                await ModuleShell.DotNetAsync(context, run.Root, ["build", root, "-c", "Release"], cancellationToken));
        }
    }
}
