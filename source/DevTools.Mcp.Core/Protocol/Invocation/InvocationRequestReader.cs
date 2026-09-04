using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Protocol.Invocation;

/// <summary>Reads <c>tools/call</c> wire params into SDK <see cref="CallToolRequestParams"/>.</summary>
public static class InvocationRequestReader
{
    public static CallToolRequestParams FromWire(JsonObject? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return new CallToolRequestParams { Name = string.Empty };

        return JsonSerializer.Deserialize<CallToolRequestParams>(parameters.ToJsonString(), ToolHelpers.ProtocolOptions)
            ?? new CallToolRequestParams { Name = string.Empty };
    }
}
