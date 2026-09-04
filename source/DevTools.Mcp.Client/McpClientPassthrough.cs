using System.Reflection;
using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client;

/// <summary>Sends one <c>tools/call</c> without the SDK client's MRTR auto-retry loop.</summary>
internal static class McpClientPassthrough
{
    private static readonly FieldInfo SessionHandlerField =
        typeof(McpClient).Assembly
            .GetType("ModelContextProtocol.Client.McpClientImpl", throwOnError: true)!
            .GetField("_sessionHandler", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("McpClientImpl._sessionHandler field not found.");

    private static readonly MethodInfo SendRequestMethod =
        SessionHandlerField.FieldType.GetMethod(
            "SendRequestAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(JsonRpcRequest), typeof(CancellationToken)],
            modifiers: null)
        ?? throw new InvalidOperationException("McpSessionHandler.SendRequestAsync not found.");

    public static async Task<JsonRpcResponse> SendAsync(
        McpClient client,
        CallToolRequestParams parameters,
        CancellationToken cancellationToken)
    {
        McpProtocol.EnsureCurrentProtocolMeta(parameters);

        var handler = SessionHandlerField.GetValue(client)!;
        var request = new JsonRpcRequest
        {
            Method = RequestMethods.ToolsCall,
            Params = JsonSerializer.SerializeToNode(
                parameters,
                ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(CallToolRequestParams))),
        };

        var task = (Task<JsonRpcResponse>)SendRequestMethod.Invoke(handler, [request, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }
}
