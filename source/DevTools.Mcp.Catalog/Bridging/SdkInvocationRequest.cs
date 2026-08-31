using System.Text.Json;
using DevTools.Mcp.Catalog.Discovery;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Bridging;

/// <summary>Builds SDK request contexts at the toolset invoke boundary.</summary>
public static class SdkInvocationRequest
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private static readonly Lazy<McpServer> LazyServer = new(CreateServer);

    public static RequestContext<CallToolRequestParams> ToToolContext(
        string toolName,
        CallToolRequestParams request,
        IServiceProvider? services = null)
    {
        request.Name = toolName;
        var jsonRpcRequest = new JsonRpcRequest
        {
            Method = RequestMethods.ToolsCall,
            Id = new RequestId("0"),
            Params = JsonSerializer.SerializeToNode(request, JsonOptions),
        };

        return new RequestContext<CallToolRequestParams>(LazyServer.Value, jsonRpcRequest, request)
        {
            Services = services,
        };
    }

    public static RequestContext<ReadResourceRequestParams> ToResourceContext(string uri)
    {
        var readParams = new ReadResourceRequestParams { Uri = uri };
        var jsonRpcRequest = new JsonRpcRequest
        {
            Method = RequestMethods.ResourcesRead,
            Id = new RequestId("0"),
            Params = JsonSerializer.SerializeToNode(readParams, JsonOptions),
        };

        return new RequestContext<ReadResourceRequestParams>(LazyServer.Value, jsonRpcRequest, readParams);
    }

    private static McpServer CreateServer() =>
        McpServer.Create(
            new SdkNoopTransport(),
            new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "DevTools.ToolsetBridge", Version = "1.0.0" },
            });
}
