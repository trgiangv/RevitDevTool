using ConsoleAppFramework;
using System.Diagnostics.CodeAnalysis;

namespace Build.Cli;

/// <summary>
///     An <see cref="InvalidOperationException" /> is a refusal: its message goes to stderr without the stack trace.
/// </summary>
[SuppressMessage("Usage", "ConsoleUse", Justification = "The command line runs before and after the pipeline.")]
internal sealed class RefusalFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        try
        {
            await Next.InvokeAsync(context, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            Environment.ExitCode = 1;
        }
    }
}
