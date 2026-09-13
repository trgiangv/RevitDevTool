using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class HostToolCallOutcomeTests
{
    [TestMethod]
    public void FromToolResult_ExposesResult()
    {
        var toolResult = new CallToolResult { Content = [new TextContentBlock { Text = "ok" }] };
        var outcome = HostToolCallOutcome.FromToolResult(toolResult);

        Assert.IsFalse(outcome.IsInputRequired);
        Assert.AreSame(toolResult, outcome.ToolResult);
        Assert.IsNull(outcome.InputRequired);
    }

    [TestMethod]
    public void FromInputRequired_ExposesInputRequired()
    {
        var inputRequired = new InputRequiredResult
        {
            RequestState = "round-1",
            InputRequests = new Dictionary<string, InputRequest>
            {
                ["confirm"] = InputRequest.ForElicitation(new ElicitRequestParams { Message = "Confirm?" }),
            },
        };
        var outcome = HostToolCallOutcome.FromInputRequired(inputRequired);

        Assert.IsTrue(outcome.IsInputRequired);
        Assert.AreSame(inputRequired, outcome.InputRequired);
        Assert.IsNull(outcome.ToolResult);
    }
}
