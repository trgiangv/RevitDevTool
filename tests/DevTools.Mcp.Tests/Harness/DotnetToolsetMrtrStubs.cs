using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Tests.Harness;

/// <summary>Same-process stub tools for ALC factory/invoker MRTR tests (T-ALC-*).</summary>
[McpServerToolType]
public static class DotnetToolsetMrtrStubs
{
    public static RequestContext<CallToolRequestParams>? LastContext { get; private set; }
    public static McpServer? LastServer { get; private set; }
    public static IProgress<ProgressNotificationValue>? LastProgress { get; private set; }
    public static string? LastName { get; private set; }
    public static bool? LastFlag { get; private set; }
    public static ClaimsPrincipal? LastUser { get; private set; }

    public static void ResetBindings()
    {
        LastContext = null;
        LastServer = null;
        LastProgress = null;
        LastName = null;
        LastFlag = null;
        LastUser = null;
    }

    [McpServerTool(Name = "bind_capture")]
    public static string BindCapture(
        RequestContext<CallToolRequestParams> context,
        McpServer server,
        IProgress<ProgressNotificationValue> progress,
        string name,
        bool flag = false)
    {
        LastContext = context;
        LastServer = server;
        LastProgress = progress;
        LastName = name;
        LastFlag = flag;
        return "ok";
    }

    [McpServerTool(Name = "bind_user")]
    public static string BindUser(ClaimsPrincipal user) =>
        (LastUser = user) is null ? "null-user" : "has-user";

    [McpServerTool(Name = "test_mrtr_confirm")]
    public static string TestMrtrConfirm(McpServer server, RequestContext<CallToolRequestParams> context)
    {
        if (context.Params?.InputResponses is { Count: > 0 } responses)
        {
            return responses.ContainsKey("confirm") ? "confirmed" : "missing_confirm_key";
        }

        if (!server.IsMrtrSupported)
            return "mrtr_unsupported";

        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                ["confirm"] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = "Confirm MRTR round?",
                    RequestedSchema = new ElicitRequestParams.RequestSchema
                    {
                        Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                        {
                            ["confirm"] = new ElicitRequestParams.BooleanSchema { Description = "Confirm" },
                        },
                    },
                }),
            },
            requestState: "demo-round1");
    }

    [McpServerTool(Name = "test_mrtr_state_a")]
    public static string TestMrtrStateA(RequestContext<CallToolRequestParams> context)
    {
        if (context.Params?.RequestState == "state-a" &&
            context.Params.InputResponses is { Count: > 0 })
            return "done-a";

        throw new InputRequiredException(requestState: "state-a");
    }

    [McpServerTool(Name = "test_mrtr_state_b")]
    public static string TestMrtrStateB(RequestContext<CallToolRequestParams> context)
    {
        if (context.Params?.RequestState == "state-b" &&
            context.Params.InputResponses is { Count: > 0 })
            return "done-b";

        throw new InputRequiredException(requestState: "state-b");
    }

    [McpServerTool(Name = "test_mrtr_structured_success")]
    public static CallToolResult TestMrtrStructuredSuccess(
        McpServer server,
        RequestContext<CallToolRequestParams> context)
    {
        if (context.Params?.InputResponses is { Count: > 0 })
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = "structured-confirmed" }],
                StructuredContent = System.Text.Json.JsonDocument.Parse("{\"ok\":true}").RootElement.Clone(),
            };
        }

        if (!server.IsMrtrSupported)
            return new CallToolResult { Content = [new TextContentBlock { Text = "mrtr_unsupported" }] };

        throw new InputRequiredException(requestState: "structured-round1");
    }

    [McpServerTool(Name = "test_mrtr_empty_responses")]
    public static string TestMrtrEmptyResponses(RequestContext<CallToolRequestParams> context)
    {
        if (context.Params?.RequestState == "empty-round1")
        {
            if (context.Params.InputResponses is null)
                return "responses_null";

            if (context.Params.InputResponses.Count == 0)
                return "responses_empty";

            return "responses_present";
        }

        throw new InputRequiredException(requestState: "empty-round1");
    }
}
