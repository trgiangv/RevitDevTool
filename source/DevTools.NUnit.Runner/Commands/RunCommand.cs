using System.Text.Json;
using DevTools.Logging;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Runner.Services;

namespace DevTools.NUnit.Runner.Commands;

public static class RunCommand
{
    public static async Task<int> ExecuteAsync(
        RunnerCommandLine options,
        HostSession hostSession,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.AssemblyPath))
        {
            await Console.Error.WriteLineAsync($"Assembly not found: {options.AssemblyPath}").ConfigureAwait(false);
            return RunnerCommandParser.ExitCliError;
        }

        if (!Enum.TryParse(options.Host, ignoreCase: true, out HostApp hostApp))
        {
            await Console.Error.WriteLineAsync($"Unsupported host '{options.Host}'.").ConfigureAwait(false);
            return RunnerCommandParser.ExitCliError;
        }

        HostPipeInstance pipe;
        try
        {
            pipe = await hostSession.EnsurePipeAsync(
                    hostApp,
                    options.Version,
                    options.HostLaunch,
                    TimeSpan.FromSeconds(options.HostLaunchTimeoutSeconds),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return RunnerCommandParser.ExitNoHost;
        }

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(options.HostTimeoutSeconds));

        try
        {
            await using var client = await NUnitPipeClient.ConnectAsync(
                    pipe.PipeName,
                    TimeSpan.FromSeconds(NUnitHostTiming.HostPipeConnectTimeoutSeconds),
                    requestTimeout.Token)
                .ConfigureAwait(false);
            await client.HelloAsync(requestTimeout.Token).ConfigureAwait(false);

            var progress = new Progress<NUnitProgressEvent>(evt =>
            {
                Console.Error.WriteLine($"[progress] {evt.Case.Name} -> {evt.Case.Outcome}");
            });

            var response = await client.RunAsync(
                options.AssemblyPath,
                options.Filter,
                waitForDebugger: false,
                progress,
                debugReady: null,
                requestTimeout.Token).ConfigureAwait(false);

            Console.WriteLine(JsonSerializer.Serialize(response, NUnitJsonContext.Default.NUnitRunResponse));

            return response.Summary.Failed > 0
                || response.Summary.Errors > 0
                || response.Summary.Cancelled > 0
                ? RunnerCommandParser.ExitTestFailure
                : RunnerCommandParser.ExitOk;
        }
        catch (IOException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return RunnerCommandParser.ExitNoHost;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync(
                    $"Host request timed out after {options.HostTimeoutSeconds}s.")
                .ConfigureAwait(false);
            return RunnerCommandParser.ExitHostTimeout;
        }
    }
}
