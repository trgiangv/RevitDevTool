using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Catalog.Tests.Harness;

internal static class DotnetToolsetTestHarness
{
    public static RequestContext<CallToolRequestParams> CreateRequest(
        Mock<McpServer>? server = null,
        IDictionary<string, JsonElement>? arguments = null,
        IDictionary<string, InputResponse>? inputResponses = null,
        string? requestState = null,
        ProgressToken? progressToken = null,
        ClaimsPrincipal? user = null)
    {
        server ??= CreateMrtrServer();
        var parameters = new CallToolRequestParams
        {
            Name = "stub",
            Arguments = arguments,
            InputResponses = inputResponses,
            RequestState = requestState,
        };
        if (progressToken is not null)
        {
            parameters.Meta = new JsonObject
            {
                ["progressToken"] = progressToken.Value.Token switch
                {
                    string s => s,
                    long l => l,
                    _ => progressToken.Value.ToString(),
                },
            };
        }

        var request = new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
            parameters);
        if (user is not null)
            request.User = user;
        return request;
    }

    public static Mock<McpServer> CreateMrtrServer(bool isMrtrSupported = true)
    {
        var server = new Mock<McpServer>();
        server.Setup(s => s.IsMrtrSupported).Returns(isMrtrSupported);
        return server;
    }

    public static Dictionary<string, JsonElement> Arguments(params (string Key, object Value)[] pairs) =>
        pairs.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value, McpJsonUtilities.DefaultOptions));

    /// <summary>Invokes parameterless static toolset spike handlers (production uses <c>McpServerTool.InvokeAsync</c>).</summary>
    public static object? InvokeRaw(MethodInfo method, RequestContext<CallToolRequestParams> request)
    {
        var result = method.Invoke(null, null);
        if (result is not Task task)
            return result;

        if (!task.IsCompleted)
        {
            throw new NotSupportedException(
                "Toolset test handler returned an incomplete async task.");
        }

        if (method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return null;
    }
}
