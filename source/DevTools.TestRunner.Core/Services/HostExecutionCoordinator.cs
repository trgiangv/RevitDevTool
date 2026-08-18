using DevTools.Hosting;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.TestRunner.Core.Services;

/// <summary>Framework-neutral outcome of acquiring a host and executing a request.</summary>
public sealed record HostExecutionResult<T>(T? Value, HostExecutionFailure? Failure, string? Error)
{
    public bool Succeeded => Failure is null;

    public static HostExecutionResult<T> Success(T value) => new(value, null, null);

    public static HostExecutionResult<T> Failed(HostExecutionFailure failure, string error) => new(default, failure, error);
}

/// <summary>Failures in the framework-neutral host execution boundary.</summary>
public enum HostExecutionFailure
{
    InvalidHost,
    NoHost,
    TimedOut,
}

/// <summary>
/// Owns host-pipe acquisition, debugger lifetime and request cancellation for runner modules.
/// Providers supply their protocol request or, for a provider-owned legacy protocol, its pipe operation.
/// </summary>
public interface IHostExecutionCoordinator
{
    Task<HostExecutionResult<T>> ExecuteAsync<T>(
        RunnerCommandContext context,
        IVisualStudioAttach debugger,
        Func<HostPipeInstance, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task<HostExecutionResult<TestingRunResponse>> RunTestingAsync(
        RunnerCommandContext context,
        TestingRunRequest request,
        IProgress<TestingCaseResult>? progress,
        TimeSpan pipeConnectTimeout,
        IVisualStudioAttach debugger,
        CancellationToken cancellationToken = default);
}

public sealed class HostExecutionCoordinator(IHostSession hosts) : IHostExecutionCoordinator
{
    public async Task<HostExecutionResult<T>> ExecuteAsync<T>(
        RunnerCommandContext context,
        IVisualStudioAttach debugger,
        Func<HostPipeInstance, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(debugger);
        ArgumentNullException.ThrowIfNull(operation);

        if (!Enum.TryParse(context.HostName, ignoreCase: true, out HostApp hostApp))
            return HostExecutionResult<T>.Failed(HostExecutionFailure.InvalidHost, $"Unsupported host '{context.HostName}'.");

        HostPipeInstance pipe;
        try
        {
            pipe = await hosts.EnsurePipeAsync(
                    hostApp,
                    context.HostVersion,
                    context.ForceLaunch,
                    TimeSpan.FromSeconds(context.LaunchTimeoutSeconds),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return HostExecutionResult<T>.Failed(HostExecutionFailure.NoHost, exception.Message);
        }

        using var debugAttach = HostDebugAttachScope.TryBegin(
            context.Debug,
            pipe.ProcessId,
            context.DebugParentPid,
            debugger,
            Console.Error);
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(context.PerTestTimeoutSeconds));

        try
        {
            return HostExecutionResult<T>.Success(
                await operation(pipe, requestTimeout.Token).ConfigureAwait(false));
        }
        catch (IOException exception)
        {
            return HostExecutionResult<T>.Failed(HostExecutionFailure.NoHost, exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HostExecutionResult<T>.Failed(
                HostExecutionFailure.TimedOut,
                $"Host request timed out after {context.PerTestTimeoutSeconds}s.");
        }
        catch (Exception exception)
        {
            return HostExecutionResult<T>.Failed(HostExecutionFailure.NoHost, exception.Message);
        }
    }

    public Task<HostExecutionResult<TestingRunResponse>> RunTestingAsync(
        RunnerCommandContext context,
        TestingRunRequest request,
        IProgress<TestingCaseResult>? progress,
        TimeSpan pipeConnectTimeout,
        IVisualStudioAttach debugger,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            context,
            debugger,
            async (pipe, requestCancellationToken) =>
            {
                await using var client = await TestingPipeClient.ConnectAsync(
                        pipe.PipeName,
                        pipeConnectTimeout,
                        requestCancellationToken)
                    .ConfigureAwait(false);
                await client.HelloAsync(request.FrameworkId, requestCancellationToken).ConfigureAwait(false);
                return await client.RunAsync(request, progress, requestCancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
}
