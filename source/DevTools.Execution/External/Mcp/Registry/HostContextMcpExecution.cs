namespace DevTools.Execution.External.Mcp.Registry;

/// <summary>Host-neutral MCP execution seam backed by the registered host context adapter.</summary>
public sealed class HostContextMcpExecution(IHostContextExecutor hostContext) : IMcpHostExecution
{
    public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken cancellationToken = default) =>
        ExecuteAsyncCore(() => hostContext.ExecuteAsync(handler, cancellationToken), cancellationToken);

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> handler, CancellationToken cancellationToken = default)
    {
        var operation = await ExecuteAsyncCore(() => hostContext.ExecuteAsync(handler, cancellationToken), cancellationToken).ConfigureAwait(false);
        return await operation.ConfigureAwait(false);
    }

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
