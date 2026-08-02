using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ToolsKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Tools;

namespace DevTools.Mcp.Catalog.Bridging;

/// <summary>Builds SDK request contexts at the toolset invoke boundary.</summary>
public static class SdkInvocationRequest
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private static readonly Lazy<McpServer> LazyServer = new(CreateServer);

    public static RequestContext<CallToolRequestParams> ToToolContext(
        string toolName,
        McpInvocationRequest request,
        IServiceProvider? services = null)
    {
        var callParams = ToCallToolParams(toolName, request);
        var jsonRpcRequest = new JsonRpcRequest
        {
            Method = McpSpecKeys.Methods.ToolsCall,
            Id = new RequestId("0"),
            Params = JsonSerializer.SerializeToNode(callParams, JsonOptions),
        };

        return new RequestContext<CallToolRequestParams>(LazyServer.Value, jsonRpcRequest, callParams)
        {
            Services = services,
        };
    }

    public static RequestContext<ReadResourceRequestParams> ToResourceContext(string uri)
    {
        var readParams = new ReadResourceRequestParams { Uri = uri };
        var jsonRpcRequest = new JsonRpcRequest
        {
            Method = McpSpecKeys.Methods.ResourcesRead,
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

    private static CallToolRequestParams ToCallToolParams(string toolName, McpInvocationRequest request)
    {
        var callParams = new CallToolRequestParams { Name = toolName };

        if (request.Arguments is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } arguments)
        {
            callParams.Arguments = arguments.ValueKind == JsonValueKind.Object
                ? arguments.EnumerateObject().ToDictionary(property => property.Name, property => property.Value)
                : new Dictionary<string, JsonElement> { [ToolsKeys.Arguments] = arguments };
        }

        if (request.InputResponses is { Count: > 0 } inputResponses)
        {
            var responses = new Dictionary<string, InputResponse>(StringComparer.Ordinal);
            foreach (var (key, value) in inputResponses)
            {
                responses[key] = new InputResponse
                {
                    RawValue = value.ValueKind is JsonValueKind.Undefined ? default : value,
                };
            }

            callParams.InputResponses = responses;
        }

        if (request.RequestState is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } requestState)
        {
            callParams.RequestState = requestState.ValueKind == JsonValueKind.String
                ? requestState.GetString()
                : requestState.GetRawText();
        }

        if (request.Meta is not null)
            callParams.Meta = request.Meta.DeepClone().AsObject();

        return callParams;
    }
}
