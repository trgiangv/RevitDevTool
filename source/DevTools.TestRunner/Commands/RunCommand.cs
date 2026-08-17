using System.Text.Json;
using DevTools.NUnit.Transport;
using DevTools.Hosting;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.TestRunner.Debugging;
using DevTools.TestRunner.Parsing;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Commands;

public static class RunCommand
{
    public static Task<int> ExecuteAsync(
        RunnerCommandLine options,
        HostSession hostSession,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(options, hostSession, debugger: null, cancellationToken);

    internal static async Task<int> ExecuteAsync(
        RunnerCommandLine options,
        HostSession hostSession,
        IVisualStudioAttach? debugger,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.AssemblyPath))
        {
            await Console.Error.WriteLineAsync($"Assembly not found: {options.AssemblyPath}").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!Enum.TryParse(options.Host, ignoreCase: true, out HostApp hostApp))
        {
            await Console.Error.WriteLineAsync($"Unsupported host '{options.Host}'.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!NUnitRunnerFilter.TryNormalize(options.Filter, out _, out var filterError))
        {
            await Console.Error.WriteLineAsync(filterError).ConfigureAwait(false);
            return RunnerExitCode.CliError;
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
            return RunnerExitCode.NoHost;
        }

        using var debugAttach = HostDebugAttachScope.TryBegin(
            options.Debug,
            pipe.ProcessId,
            options.DebugParentPid,
            debugger ?? VisualStudioAttach.Instance,
            Console.Error);

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
                progress,
                requestTimeout.Token).ConfigureAwait(false);

            Console.WriteLine(JsonSerializer.Serialize(response, NUnitJsonContext.Default.NUnitRunResponse));

            return response.Summary.Failed > 0
                || response.Summary.Errors > 0
                || response.Summary.Cancelled > 0
                ? RunnerExitCode.TestFailure
                : RunnerExitCode.Ok;
        }
        catch (IOException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return RunnerExitCode.NoHost;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync(
                    $"Host request timed out after {options.HostTimeoutSeconds}s.")
                .ConfigureAwait(false);
            return RunnerExitCode.HostTimeout;
        }
    }
}
