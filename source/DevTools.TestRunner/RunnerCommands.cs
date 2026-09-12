using System.Text.Json;
using ConsoleAppFramework;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Debugging;
using DevTools.TestRunner.Parsing;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner;

public sealed class RunnerCommands(
    ITestCoordinator execution,
    IDebuggerAttach debugger,
    IRunInput runInput)
{
    /// <summary>
    /// Adapter → Runner entry. Stdin is one <see cref="TestRunExecute"/>
    /// JSON document; <see cref="TestRunRequest.RunId"/> is preserved to the host.
    /// </summary>
    [Command("run")]
    public async Task<int> Run(CancellationToken cancellationToken = default)
    {
        var json = await runInput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        TestRunExecute? execute;
        try
        {
            execute = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);
        }
        catch (JsonException exception)
        {
            await Console.Error.WriteLineAsync($"Invalid run JSON: {exception.Message}").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (execute is null)
        {
            await Console.Error.WriteLineAsync("run stdin was empty.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!TestingProtocol.IsCompatible(execute.ProtocolVersion)
            || !TestingProtocol.IsCompatible(execute.Run.ProtocolVersion))
        {
            var version = TestingProtocol.IsCompatible(execute.ProtocolVersion)
                ? execute.Run.ProtocolVersion
                : execute.ProtocolVersion;
            await Console.Error.WriteLineAsync(TestingProtocol.CreateUnsupportedMessage(version))
                .ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!File.Exists(execute.Run.Assembly.Path))
        {
            await Console.Error.WriteLineAsync($"Assembly not found: {execute.Run.Assembly.Path}")
                .ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (RunnerCommandContext.TryCreate(
                execute.Host.HostName, 
                execute.Host.HostVersion, 
                execute.Host.ForceLaunch, 
                execute.Host.PerTestTimeoutSeconds, 
                execute.Host.LaunchTimeoutSeconds, 
                execute.Host.DebugParentPid is > 0, 
                execute.Host.DebugParentPid, 
                execute.Host.EffectiveRequestTimeoutSeconds, 
                out var context, 
                out var error)) 
            return await Execute(execute, context!, cancellationToken).ConfigureAwait(false);
        
        await Console.Error.WriteLineAsync(error ?? "Invalid run JSON.").ConfigureAwait(false);
        return RunnerExitCode.CliError;
    }

    private async Task<int> Execute(
        TestRunExecute execute,
        RunnerCommandContext context,
        CancellationToken cancellationToken)
    {
        using var cancelSignal = TestCancelSignal.Create(execute.Run.RunId);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var stopCancelMonitor = new CancellationTokenSource();
        var cancelMonitor = MonitorCancelSignalAsync(cancelSignal, linked, stopCancelMonitor.Token);

        var progress = new Progress<TestEvent>(testingEvent =>
        {
            Console.WriteLine(
                JsonSerializer.Serialize(
                    new TestRunnerStreamMessage(Event: testingEvent),
                    TestingJsonContext.Default.TestRunnerStreamMessage));
        });

        ExecutionResult<TestRunResponse> result;
        try
        {
            result = await execution.ExecuteAsync(
                    context,
                    debugger,
                    async (pipe, requestCancellationToken) =>
                    {
                        await using var client = await TestPipeClient.ConnectAsync(
                                pipe.PipeName,
                                TimeSpan.FromSeconds(TestHostTiming.HostPipeConnectTimeoutSeconds),
                                requestCancellationToken)
                            .ConfigureAwait(false);
                        var hello = await client.HelloAsync(execute.Run.FrameworkId, requestCancellationToken)
                            .ConfigureAwait(false);
                        EnsureHelloMatches(hello, execute);
                        return await client.RunAsync(execute.Run, progress, requestCancellationToken)
                            .ConfigureAwait(false);
                    },
                    linked.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            await stopCancelMonitor.CancelAsync().ConfigureAwait(false);
            await cancelMonitor.ConfigureAwait(false);
        }

        if (!result.Succeeded)
        {
            await Console.Error.WriteLineAsync(result.Error ?? "Host execution failed.").ConfigureAwait(false);
            return result.Failure switch
            {
                ExecutionFailure.InvalidHost => RunnerExitCode.CliError,
                ExecutionFailure.TimedOut => RunnerExitCode.RequestTimeout,
                _ => RunnerExitCode.NoHost
            };
        }

        Console.WriteLine(
            JsonSerializer.Serialize(
                new TestRunnerStreamMessage(Response: result.Value),
                TestingJsonContext.Default.TestRunnerStreamMessage));

        return HasRunFailure(result.Value!) ? RunnerExitCode.TestFailure : RunnerExitCode.Ok;
    }

    private static async Task MonitorCancelSignalAsync(
        EventWaitHandle cancelSignal,
        CancellationTokenSource linked,
        CancellationToken stopMonitoring)
    {
        var signaled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ThreadPool.RegisterWaitForSingleObject(
            cancelSignal,
            static (state, _) => ((TaskCompletionSource)state!).TrySetResult(),
            signaled,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: true);

        try
        {
            await signaled.Task.WaitAsync(stopMonitoring).ConfigureAwait(false);
            await linked.CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopMonitoring.IsCancellationRequested)
        {
        }
        finally
        {
            registration.Unregister(null);
        }
    }

    private static void EnsureHelloMatches(TestHelloResponse hello, TestRunExecute execute)
    {
        if (hello.FrameworkId != execute.Run.FrameworkId)
        {
            throw new InvalidOperationException(
                $"Host framework '{hello.FrameworkId}' does not match '{execute.Run.FrameworkId}'.");
        }

        if (!string.Equals(hello.Host, execute.Host.HostName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Host '{hello.Host}' does not match requested '{execute.Host.HostName}'.");
        }

        if (!string.Equals(hello.HostVersion, execute.Host.HostVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Host version '{hello.HostVersion}' does not match requested '{execute.Host.HostVersion}'.");
        }

        if (hello.IsBusy)
            throw new InvalidOperationException("Host testing session is busy.");
    }

    private static bool HasRunFailure(TestRunResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.DiagnosticCode))
            return true;

        if (response.CancellationState is TestCancellationState.Completed
            or TestCancellationState.Poisoned)
            return true;

        return response.Results.Any(result =>
            string.Equals(result.Outcome, TestOutcomes.Failed, StringComparison.Ordinal)
            || string.Equals(result.Outcome, TestOutcomes.Error, StringComparison.Ordinal)
            || string.Equals(result.Outcome, TestOutcomes.Cancelled, StringComparison.Ordinal));
    }
}
