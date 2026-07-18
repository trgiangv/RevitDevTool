using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Registry;

/// <summary>Runs an in-host MCP primitive on the active host API context.</summary>
public interface IMcpHostExecution
{
    Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(Func<Task<T>> handler, CancellationToken cancellationToken = default);
}

/// <summary>Decorates SDK primitives so every in-host invocation crosses the host execution boundary.</summary>
public static class McpHostExecutionPrimitives
{
    public static McpServerTool Wrap(McpServerTool primitive, IMcpHostExecution execution) => new HostTool(primitive, execution);
    public static McpServerPrompt Wrap(McpServerPrompt primitive, IMcpHostExecution execution) => new HostPrompt(primitive, execution);
    public static McpServerResource Wrap(McpServerResource primitive, IMcpHostExecution execution) => new HostResource(primitive, execution);

    private sealed class HostTool(McpServerTool primitive, IMcpHostExecution execution) : McpServerTool
    {
        public override Tool ProtocolTool => primitive.ProtocolTool;
        public override IReadOnlyList<object> Metadata => primitive.Metadata;

        public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await execution.ExecuteAsync(() => primitive.InvokeAsync(request, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class HostPrompt(McpServerPrompt primitive, IMcpHostExecution execution) : McpServerPrompt
    {
        public override Prompt ProtocolPrompt => primitive.ProtocolPrompt;
        public override IReadOnlyList<object> Metadata => primitive.Metadata;

        public override async ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await execution.ExecuteAsync(() => primitive.GetAsync(request, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class HostResource(McpServerResource primitive, IMcpHostExecution execution) : McpServerResource
    {
        public override Resource? ProtocolResource => primitive.ProtocolResource;
        public override ResourceTemplate ProtocolResourceTemplate => primitive.ProtocolResourceTemplate;
        public override IReadOnlyList<object> Metadata => primitive.Metadata;
        public override bool IsMatch(string uri) => primitive.IsMatch(uri);

        public override async ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await execution.ExecuteAsync(() => primitive.ReadAsync(request, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false);
        }
    }
}
