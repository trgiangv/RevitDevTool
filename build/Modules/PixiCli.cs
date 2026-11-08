using ModularPipelines.Context;
using ModularPipelines.Models;
using ModularPipelines.Options;
using System.ComponentModel;

namespace Build.Modules;

internal static class PixiCli
{
    public const string Project = "source/RevitDevTool.PyTest";

    public static async Task<CommandResult> RunAsync(IModuleContext context, string root, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await context.Shell.Command.ExecuteCommandLineTool(
                new GenericCommandLineToolOptions("pixi")
                {
                    Arguments = ["run", "--manifest-path", Path.Combine(root, Project, "pixi.toml"), .. arguments]
                },
                new CommandExecutionOptions
                {
                    WorkingDirectory = Path.Combine(root, Project)
                },
                cancellationToken: cancellationToken);
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException("pixi is not on PATH; install it from https://pixi.sh to build the pytest train.");
        }
    }
}
