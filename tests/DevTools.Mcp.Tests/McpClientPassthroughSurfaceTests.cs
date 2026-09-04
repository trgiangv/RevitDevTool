using System.Reflection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

/// <summary>
/// Guards <see cref="DevTools.Mcp.Client.McpClientPassthrough"/> reflection against SDK 2.2.0 surface drift (ADR 0027).
/// </summary>
public sealed class McpClientPassthroughSurfaceTests
{
    private const string FailurePrefix =
        "MCP SDK 2.2.0 passthrough surface changed (see ADR 0027 / docs/decisions/0027-mcp-product-surface.md): ";

    [Fact]
    public void McpClientImpl_Exposes_SessionHandler_And_SendRequestAsync()
    {
        var clientImplType = typeof(McpClient).Assembly
            .GetType("ModelContextProtocol.Client.McpClientImpl", throwOnError: false);

        Assert.NotNull(clientImplType);
        Assert.False(clientImplType!.IsAbstract);

        var sessionHandlerField = clientImplType.GetField(
            "_sessionHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(sessionHandlerField);
        Assert.False(
            sessionHandlerField!.IsStatic,
            FailurePrefix + "McpClientImpl._sessionHandler instance field not found.");

        var sendRequestMethod = sessionHandlerField.FieldType.GetMethod(
            "SendRequestAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(JsonRpcRequest), typeof(CancellationToken)],
            modifiers: null);

        Assert.True(
            sendRequestMethod is not null,
            FailurePrefix + "SendRequestAsync(JsonRpcRequest, CancellationToken) not found on session handler.");
        Assert.Equal(
            typeof(Task<JsonRpcResponse>),
            sendRequestMethod!.ReturnType);
    }
}
