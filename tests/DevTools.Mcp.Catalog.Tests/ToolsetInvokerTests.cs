using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Catalog.Tests.Harness;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Catalog.Tests;

/// <summary>ToolsetInvoker MRTR round-trip and catalog propagation (T-ALC-10..15).</summary>
[TestClass]
public sealed class ToolsetInvokerTests
{
    private static MethodInfo MrtrConfirmMethod() =>
        typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.TestMrtrConfirm))!;

    [TestMethod]
    public void T_ALC_10_Round1_NoInputResponses_ThrowsInputRequiredException()
    {
        var request = DotnetToolsetTestHarness.CreateRequest();

        var ex = DotnetToolsetTestHarness.InvokeExpectingInputRequired(MrtrConfirmMethod(), request);

        Assert.IsNotNull(ex.Result.InputRequests);
        Assert.Contains("confirm", ex.Result.InputRequests!.Keys);
        Assert.AreEqual("demo-round1", ex.Result.RequestState);
    }

    [TestMethod]
    public void T_ALC_11_Round2_InputResponsesAndRequestState_ReturnsSuccess()
    {
        var request = DotnetToolsetTestHarness.CreateRequest(
            inputResponses: new Dictionary<string, InputResponse>
            {
                ["confirm"] = InputResponse.FromElicitResult(new ElicitResult { Action = "accept" }),
            },
            requestState: "demo-round1");

        var result = Assert.IsInstanceOfType<string>(DotnetToolsetTestHarness.InvokeRaw(MrtrConfirmMethod(), request));

        Assert.AreEqual("confirmed", result);
    }

    [TestMethod]
    public void T_ALC_12_Round2Result_SurvivesResultSerializerRoundTrip()
    {
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.TestMrtrStructuredSuccess))!;
        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var request = DotnetToolsetTestHarness.CreateRequest(
            inputResponses: new Dictionary<string, InputResponse>
            {
                ["confirm"] = InputResponse.FromElicitResult(new ElicitResult { Action = "accept" }),
            },
            requestState: "structured-round1");

        var result = DotnetToolsetTestHarness.InvokeToResponse(method, request, outputSchema);

        Assert.AreEqual("structured-confirmed", McpToolInvoke.Text(result));
        Assert.IsTrue(result.StructuredContent!.Value.GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public async Task T_ALC_13_Handler_ReturnsInputRequiredJsonShape()
    {
        var inputRequired = new InputRequiredException(requestState: "catalog-round1");
        var dispatcher = new Mock<IMcpPrimitiveDispatcher>();
        dispatcher
            .Setup(d => d.DispatchToolAsync(
                It.IsAny<McpRegisteredTool>(),
                It.IsAny<CallToolRequestParams>(),
                It.IsAny<IHostContextExecutor>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpResult<McpInvocationResponse>.Success(
                ToolsetMrtrBridge.ToInputRequiredResponse(inputRequired)));

        var catalogStore = McpHostTestHarness.CreateCatalogStore(McpHostTestHarness.CreateRegisteredTool("mrtr_tool"));
        var handler = McpHostTestHarness.CreateHandler(catalogStore, dispatcher);

        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateRequest(RequestMethods.ToolsCall, new JsonObject { ["name"] = "mrtr_tool" }, id: 2),
            CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.IsNull(response!["error"]);
        Assert.AreEqual("catalog-round1", response!["result"]!["requestState"]!.GetValue<string>());
    }

    [TestMethod]
    public void T_ALC_14_RetryMissingInputResponsesKey_ReturnsToolAuthoredPolicy()
    {
        var request = DotnetToolsetTestHarness.CreateRequest(
            inputResponses: new Dictionary<string, InputResponse>
            {
                ["other"] = new InputResponse(),
            },
            requestState: "demo-round1");

        var result = Assert.IsInstanceOfType<string>(DotnetToolsetTestHarness.InvokeRaw(MrtrConfirmMethod(), request));

        Assert.AreEqual("missing_confirm_key", result);
    }

    [TestMethod]
    public void T_ALC_14_EmptyInputResponsesDictionary_ReturnsToolAuthoredPolicy()
    {
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.TestMrtrEmptyResponses))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            inputResponses: new Dictionary<string, InputResponse>(),
            requestState: "empty-round1");

        var result = Assert.IsInstanceOfType<string>(DotnetToolsetTestHarness.InvokeRaw(method, request));

        Assert.AreEqual("responses_empty", result);
    }

    [TestMethod]
    public void T_ALC_14_RequestStateEchoedWithNullInputResponses_ReturnsToolAuthoredPolicy()
    {
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.TestMrtrEmptyResponses))!;
        var request = DotnetToolsetTestHarness.CreateRequest(
            requestState: "empty-round1");

        var result = Assert.IsInstanceOfType<string>(DotnetToolsetTestHarness.InvokeRaw(method, request));

        Assert.AreEqual("responses_null", result);
    }

    [TestMethod]
    public void T_ALC_10_IsMrtrUnsupported_ReturnsSoftStringInsteadOfThrow()
    {
        var request = DotnetToolsetTestHarness.CreateRequest(
            server: DotnetToolsetTestHarness.CreateMrtrServer(isMrtrSupported: false));

        var result = Assert.IsInstanceOfType<string>(DotnetToolsetTestHarness.InvokeRaw(MrtrConfirmMethod(), request));

        Assert.AreEqual("mrtr_unsupported", result);
    }

    [TestMethod]
    public void T_ALC_11_SequentialMrtrTools_DoNotCrossContaminateRequestState()
    {
        var methodA = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.TestMrtrStateA))!;
        var methodB = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.TestMrtrStateB))!;

        var round1A = DotnetToolsetTestHarness.InvokeExpectingInputRequired(
            methodA,
            DotnetToolsetTestHarness.CreateRequest());
        var round1B = DotnetToolsetTestHarness.InvokeExpectingInputRequired(
            methodB,
            DotnetToolsetTestHarness.CreateRequest());

        Assert.AreEqual("state-a", round1A.Result.RequestState);
        Assert.AreEqual("state-b", round1B.Result.RequestState);

        var resultA = Assert.IsInstanceOfType<string>(DotnetToolsetTestHarness.InvokeRaw(
            methodA,
            DotnetToolsetTestHarness.CreateRequest(
                inputResponses: new Dictionary<string, InputResponse> { ["x"] = new() },
                requestState: "state-a")));
        var resultB = Assert.IsInstanceOfType<string>(DotnetToolsetTestHarness.InvokeRaw(
            methodB,
            DotnetToolsetTestHarness.CreateRequest(
                inputResponses: new Dictionary<string, InputResponse> { ["x"] = new() },
                requestState: "state-b")));

        Assert.AreEqual("done-a", resultA);
        Assert.AreEqual("done-b", resultB);
    }

    [TestMethod]
    public void T_ALC_15_MrtrSuccessCallToolResult_StillMapsThroughResultSerializer()
    {
        var method = typeof(DotnetToolsetMrtrStubs).GetMethod(nameof(DotnetToolsetMrtrStubs.TestMrtrStructuredSuccess))!;
        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var request = DotnetToolsetTestHarness.CreateRequest(
            inputResponses: new Dictionary<string, InputResponse> { ["confirm"] = new() },
            requestState: "structured-round1");

        var raw = DotnetToolsetTestHarness.InvokeRaw(method, request);
        var mapped = ToolsetResultSerializer.ToInvocationResponse(raw, outputSchema);

        Assert.IsInstanceOfType<CallToolResult>(raw);
        Assert.AreEqual("structured-confirmed", McpToolInvoke.Text(mapped));
        Assert.IsNotNull(mapped.StructuredContent);
    }
}
