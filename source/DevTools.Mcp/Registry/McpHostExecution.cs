using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Registry;

/// <summary>Runs an in-host MCP primitive on the active host API context.</summary>
public interface IMcpHostExecution
{
    Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken cancellationToken = default);
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
            return await execution.ExecuteAsync(
                () => GetCompletedResult(primitive.InvokeAsync(request, cancellationToken), "tool", primitive.ProtocolTool.Name),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class HostPrompt(McpServerPrompt primitive, IMcpHostExecution execution) : McpServerPrompt
    {
        public override Prompt ProtocolPrompt => primitive.ProtocolPrompt;
        public override IReadOnlyList<object> Metadata => primitive.Metadata;

        public override async ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await execution.ExecuteAsync(
                () => GetCompletedResult(primitive.GetAsync(request, cancellationToken), "prompt", primitive.ProtocolPrompt.Name),
                cancellationToken).ConfigureAwait(false);
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
            var name = primitive.ProtocolResource?.Name ?? primitive.ProtocolResourceTemplate.Name;
            return await execution.ExecuteAsync(
                () => GetCompletedResult(primitive.ReadAsync(request, cancellationToken), "resource", name),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static T GetCompletedResult<T>(ValueTask<T> operation, string kind, string name)
    {
        if (!operation.IsCompleted)
            throw new InvalidOperationException($"MCP {kind} '{name}' returned an incomplete asynchronous result. Host-context MCP primitives must complete synchronously.");

        return operation.GetAwaiter().GetResult();
    }
}
