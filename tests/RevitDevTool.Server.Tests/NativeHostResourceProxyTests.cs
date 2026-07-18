using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Native;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class NativeHostResourceProxyTests
{
    [Fact]
    public async Task NativeResourceProxies_ExposeAndResolveDirectAndTemplateUris()
    {
        var session = new RecordingResourceSession();
        var direct = new NativeHostResourceProxy(session, new Resource
        {
            Name = "model_context",
            Uri = "revit://model/context"
        }, null);
        var template = new NativeHostResourceProxy(session, null, new ResourceTemplate
        {
            Name = "element",
            UriTemplate = "revit://model/elements/{id}"
        });

        Assert.Equal("devtools://host/6101/resource/cmV2aXQ6Ly9tb2RlbC9jb250ZXh0", direct.ProtocolResource!.Uri);
        Assert.Contains("{id}", template.ProtocolResourceTemplate.UriTemplate);

        var requestedUri = template.ProtocolResourceTemplate.UriTemplate.Replace("{id}", "42", StringComparison.Ordinal);
        Assert.True(template.IsMatch(requestedUri));
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StreamServerTransport(input, output);
        await using var server = McpServer.Create(transport, new McpServerOptions());

        await direct.ReadAsync(CreateRequest(server,
            new ReadResourceRequestParams { Uri = direct.ProtocolResource.Uri }, RequestMethods.ResourcesRead),
            TestContext.Current.CancellationToken);
        await template.ReadAsync(CreateRequest(server,
            new ReadResourceRequestParams { Uri = requestedUri }, RequestMethods.ResourcesRead),
            TestContext.Current.CancellationToken);

        Assert.Equal(["revit://model/context", "revit://model/elements/42"], session.RequestedUris);
    }

    private static RequestContext<T> CreateRequest<T>(McpServer server, T parameters, string method) =>
        new(server, new JsonRpcRequest { Id = new RequestId(Guid.NewGuid().ToString("N")), Method = method }, parameters);

    private sealed class RecordingResourceSession : IHostMcpSession
    {
        public HostInstanceDescriptor Instance { get; } = new(6101, "TestHost", "1.0", McpPipeName.Format(6101));
        public bool IsConnected => true;
        public List<string> RequestedUris { get; } = [];
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientTool>>([]);
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResource>>([]);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResourceTemplate>>([]);
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => throw new NotSupportedException();

        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct)
        {
            RequestedUris.Add(uri);
            return Task.FromResult(new ReadResourceResult());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
