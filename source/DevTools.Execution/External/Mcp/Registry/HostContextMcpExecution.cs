namespace DevTools.Execution.External.Mcp.Registry;

/// <summary>Host-neutral MCP execution seam backed by the registered host context adapter.</summary>
public sealed class HostContextMcpExecution(IHostContextExecutor hostContext) : IMcpHostExecution
{
    public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken cancellationToken = default) =>
        ExecuteAsyncCore(() => hostContext.ExecuteAsync(handler, cancellationToken), cancellationToken);

    private static async Task<T> ExecuteAsyncCore<T>(Func<Task<T>> execute, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previousMode = ExecutionGuardContext.Mode;
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        try
        {
            return await execute().ConfigureAwait(false);
        }
        finally
        {
            ExecutionGuardContext.Mode = previousMode;
        }
    }
}
