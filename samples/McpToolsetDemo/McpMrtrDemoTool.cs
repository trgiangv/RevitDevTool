using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpToolsetDemo;

/// <summary>
/// Optional live/spike stub for ALC low-level MRTR (T-HOST-02). Not a product tool.
/// Requires shared host <c>ModelContextProtocol*</c> identity (MCP excluded from toolset ILRepack).
/// </summary>
[McpServerToolType]
public static class McpMrtrDemoTool
{
    private const string DemoRequestState = "demo-round1";

    [McpServerTool(
        Name = "test_mrtr_confirm",
        Title = "Test MRTR Confirm",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Conformance-style stub: low-level MRTR confirm via InputRequiredException. " +
        "Soft message when MRTR is not negotiated; throw when IsMrtrSupported.")]
    public static string TestMrtrConfirm(
        McpServer server,
        RequestContext<CallToolRequestParams> context)
    {
        if (context.Params?.InputResponses is { Count: > 0 })
            return "confirmed";

        if (!server.IsMrtrSupported)
            return "This tool requires MRTR support.";

        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                ["confirm"] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = "Confirm MRTR demo round?",
                    RequestedSchema = new ElicitRequestParams.RequestSchema
                    {
                        Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                        {
                            ["ok"] = new ElicitRequestParams.BooleanSchema
                            {
                                Description = "Accept to complete the MRTR round-trip.",
                            },
                        },
                        Required = ["ok"],
                    },
                }),
            },
            requestState: DemoRequestState);
    }
}
