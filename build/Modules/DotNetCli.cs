using ModularPipelines.Context;
using ModularPipelines.Models;
using ModularPipelines.Options;

namespace Build.Modules;

internal static class DotNetCli
{
    public static Task<CommandResult> RunAsync(IModuleContext context, string workingDirectory, IEnumerable<string> arguments, CancellationToken cancellationToken, bool throwOnError = true) =>
        context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions("dotnet")
            {
                Arguments = arguments.ToArray()
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = workingDirectory,
                ThrowOnNonZeroExitCode = throwOnError
            },
            cancellationToken: cancellationToken);
}
