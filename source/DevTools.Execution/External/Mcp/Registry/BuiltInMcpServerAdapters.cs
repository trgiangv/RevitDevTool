using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Registry;

/// <summary>Temporary SDK bindings for legacy built-ins; Task 7 replaces these with typed primitives.</summary>
internal sealed class BuiltInMcpServerAdapters(
    IEnumerable<IBuiltInMcpTool> tools,
    IEnumerable<IBuiltInMcpPrompt> prompts,
    IEnumerable<IBuiltInMcpResource> resources) : IMcpServerPrimitiveAdapter
{
    private readonly IReadOnlyDictionary<string, IBuiltInMcpTool> _tools = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, IBuiltInMcpPrompt> _prompts = prompts.ToDictionary(prompt => prompt.ProtocolPrompt.Name, StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, IBuiltInMcpResource> _resources = resources.ToDictionary(resource => resource.ProtocolResource.Uri, StringComparer.OrdinalIgnoreCase);

    public ExecutionMode SourceKind => ExecutionMode.CSharp;

    public McpServerTool? CreateTool(McpRegisteredTool registration) =>
        _tools.TryGetValue(registration.ProtocolTool.Name, out var tool) ? new BuiltInMcpServerTool(tool) : null;

    public McpServerPrompt? CreatePrompt(McpRegisteredPrompt registration) =>
        _prompts.TryGetValue(registration.ProtocolPrompt.Name, out var prompt) ? new BuiltInMcpServerPrompt(prompt) : null;

    public McpServerResource? CreateResource(McpRegisteredResource registration)
    {
        var key = registration.ProtocolResource?.Uri ?? registration.ProtocolTemplate?.UriTemplate;
        return key is not null && _resources.TryGetValue(key, out var resource) ? new BuiltInMcpServerResource(resource) : null;
    }

    private sealed class BuiltInMcpServerTool(IBuiltInMcpTool tool) : McpServerTool
    {
        public override Tool ProtocolTool => tool.ProtocolTool;
        public override IReadOnlyList<object> Metadata => [];

        public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = JsonSerializer.Serialize(request.Params.Arguments ?? new Dictionary<string, JsonElement>(), McpJsonUtilities.DefaultOptions);
            var result = await ExecuteWithSuppressedGuardAsync(
                () => tool.ExecuteAsync(payload, cancellationToken)).ConfigureAwait(false);
            return result.Result ?? new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = result.Error?.Message ?? result.Detail }]
            };
        }
    }

    private sealed class BuiltInMcpServerPrompt(IBuiltInMcpPrompt prompt) : McpServerPrompt
    {
        public override Prompt ProtocolPrompt => prompt.ProtocolPrompt;
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arguments = request.Params.Arguments?.ToDictionary(pair => pair.Key, pair => pair.Value);
            return new ValueTask<GetPromptResult>(ExecuteWithSuppressedGuard(() => prompt.Get(arguments)));
        }
    }

    private sealed class BuiltInMcpServerResource(IBuiltInMcpResource resource) : McpServerResource
    {
        public override Resource? ProtocolResource => resource.ProtocolResource;
        public override ResourceTemplate ProtocolResourceTemplate => new()
        {
            UriTemplate = resource.ProtocolResource.Uri,
            Name = resource.ProtocolResource.Name,
            Description = resource.ProtocolResource.Description
        };
        public override IReadOnlyList<object> Metadata => [];
        public override bool IsMatch(string uri) => string.Equals(resource.ProtocolResource.Uri, uri, StringComparison.OrdinalIgnoreCase);
        public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<ReadResourceResult>(ExecuteWithSuppressedGuard(() => resource.Read(request.Params.Uri)));
        }
    }

    private static async Task<T> ExecuteWithSuppressedGuardAsync<T>(Func<Task<T>> operation)
    {
        var previousMode = ExecutionGuardContext.Mode;
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            ExecutionGuardContext.Mode = previousMode;
        }
    }

    private static T ExecuteWithSuppressedGuard<T>(Func<T> operation)
    {
        var previousMode = ExecutionGuardContext.Mode;
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        try
        {
            return operation();
        }
        finally
        {
            ExecutionGuardContext.Mode = previousMode;
        }
    }
}
