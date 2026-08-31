using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Protocol.Invocation;

/// <summary>Reads <c>tools/call</c> wire params into SDK <see cref="CallToolRequestParams"/>.</summary>
public static class InvocationRequestReader
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    public static CallToolRequestParams FromWire(JsonObject? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return new CallToolRequestParams { Name = string.Empty };

        return JsonSerializer.Deserialize<CallToolRequestParams>(parameters.ToJsonString(), JsonOptions)
            ?? new CallToolRequestParams { Name = string.Empty };
    }
}
