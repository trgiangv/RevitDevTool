using DevTools.Hosting;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;

namespace DevTools.TestRunner.Core.Services;

/// <summary>Framework-neutral outcome of acquiring a host and executing a request.</summary>
public sealed record ExecutionResult<T>(T? Value, ExecutionFailure? Failure, string? Error)
{
    public bool Succeeded => Failure is null;

    public static ExecutionResult<T> Success(T value) => new(value, null, null);

    public static ExecutionResult<T> Failed(ExecutionFailure failure, string error) => new(default, failure, error);
}

/// <summary>Failures in the framework-neutral execution boundary.</summary>
public enum ExecutionFailure
{
    InvalidHost,
    NoHost,
    TimedOut,
}

/// <summary>
/// Owns host-pipe acquisition, debugger lifetime and request cancellation.
/// The caller supplies the pipe operation (CLI sends <c>testing/run</c>).
/// </summary>
public interface IExecutionCoordinator
{
    Task<ExecutionResult<T>> ExecuteAsync<T>(
        RunnerCommandContext context,
        IDebuggerAttach debugger,
        Func<HostPipeInstance, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public sealed class ExecutionCoordinator(ITestSession session) : IExecutionCoordinator
{
    public async Task<ExecutionResult<T>> ExecuteAsync<T>(
        RunnerCommandContext context,
        IDebuggerAttach debugger,
        Func<HostPipeInstance, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(debugger);
        ArgumentNullException.ThrowIfNull(operation);

        if (!Enum.TryParse(context.HostName, ignoreCase: true, out HostApp hostApp))
            return ExecutionResult<T>.Failed(ExecutionFailure.InvalidHost, $"Unsupported host '{context.HostName}'.");

        await using var sessionLifetime = DebugHostLifetime.Link(context.DebugParentPid, cancellationToken);
        HostPipeInstance pipe;
        try
        {
            pipe = await session.EnsurePipeAsync(
                    hostApp,
                    context.HostVersion,
                    context.ForceLaunch,
                    TimeSpan.FromSeconds(context.LaunchTimeoutSeconds),
                    sessionLifetime.Token)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return ExecutionResult<T>.Failed(ExecutionFailure.NoHost, exception.Message);
        }

        using var debugAttach = DebugAttachScope.TryBegin(
            context.Debug,
            new AttachTarget(
                pipe.ProcessId,
                context.DebugParentPid,
                context.AssemblyPath),
            debugger,
            Console.Error);
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(sessionLifetime.Token);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(context.PerTestTimeoutSeconds));

        try
        {
            return ExecutionResult<T>.Success(
                await operation(pipe, requestTimeout.Token).ConfigureAwait(false));
        }
        catch (IOException exception)
        {
            return ExecutionResult<T>.Failed(ExecutionFailure.NoHost, exception.Message);
        }
        catch (OperationCanceledException) when (!sessionLifetime.IsCancellationRequested)
        {
            return ExecutionResult<T>.Failed(
                ExecutionFailure.TimedOut,
                $"Host request timed out after {context.PerTestTimeoutSeconds}s.");
        }
        catch (Exception exception)
        {
            return ExecutionResult<T>.Failed(ExecutionFailure.NoHost, exception.Message);
        }
    }
}
