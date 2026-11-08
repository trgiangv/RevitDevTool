using ModularPipelines.Context;
using ModularPipelines.Models;
using ModularPipelines.Options;
using System.ComponentModel;

namespace Build.Tools;

/// <summary>
///     Runs <c>dotnet</c> and <c>pixi</c> inside a pipeline module, so the output lands in the module's log.
/// </summary>
internal static class ModuleShell
{
    public const string PyTestProject = "source/RevitDevTool.PyTest";

    public static Task<CommandResult> DotNetAsync(IModuleContext context, string root, IEnumerable<string> arguments, CancellationToken cancellationToken, bool throwOnError = true) =>
        RunAsync(context, "dotnet", root, arguments, cancellationToken, throwOnError);

    public static async Task<CommandResult> PixiAsync(IModuleContext context, string root, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var project = Path.Combine(root, PyTestProject);
        try
        {
            return await RunAsync(context, "pixi", project, ["run", "--manifest-path", Path.Combine(project, "pyproject.toml"), .. arguments], cancellationToken, throwOnError: true);
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException("pixi is not on PATH; install it from https://pixi.sh to build the pytest product.");
        }
    }

    private static Task<CommandResult> RunAsync(IModuleContext context, string tool, string workingDirectory, IEnumerable<string> arguments, CancellationToken cancellationToken, bool throwOnError) =>
        context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions(tool) { Arguments = arguments.ToArray() },
            new CommandExecutionOptions { WorkingDirectory = workingDirectory, ThrowOnNonZeroExitCode = throwOnError },
            cancellationToken: cancellationToken);
}
