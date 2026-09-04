using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

public sealed class HostToolCallOutcomeTests
{
    [Fact]
    public void FromToolResult_ExposesResult()
    {
        var toolResult = new CallToolResult { Content = [new TextContentBlock { Text = "ok" }] };
        var outcome = HostToolCallOutcome.FromToolResult(toolResult);

        Assert.False(outcome.IsInputRequired);
        Assert.Same(toolResult, outcome.ToolResult);
        Assert.Null(outcome.InputRequired);
    }

    [Fact]
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

        Assert.True(outcome.IsInputRequired);
        Assert.Same(inputRequired, outcome.InputRequired);
        Assert.Null(outcome.ToolResult);
    }
}
