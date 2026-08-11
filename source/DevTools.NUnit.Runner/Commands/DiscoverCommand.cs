using System.Text.Json;
using DevTools.Logging;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Runner.Services;

namespace DevTools.NUnit.Runner.Commands;

public static class DiscoverCommand
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

        if (!TryParseHost(options.Host, out var hostApp, out var hostError))
        {
            await Console.Error.WriteLineAsync(hostError).ConfigureAwait(false);
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
            var response = await client.DiscoverAsync(options.AssemblyPath, options.Filter, requestTimeout.Token)
                .ConfigureAwait(false);

            Console.WriteLine(JsonSerializer.Serialize(response, NUnitJsonContext.Default.NUnitDiscoverResponse));
            return RunnerCommandParser.ExitOk;
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

    private static bool TryParseHost(string host, out HostApp hostApp, out string error)
    {
        if (Enum.TryParse(host, ignoreCase: true, out hostApp))
        {
            error = string.Empty;
            return true;
        }

        hostApp = default;
        error = $"Unsupported host '{host}'.";
        return false;
    }
}
