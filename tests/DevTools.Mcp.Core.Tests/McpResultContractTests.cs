using DevTools.Mcp.Core.Results;

namespace DevTools.Mcp.Core.Tests;

public sealed class McpResultContractTests
{
    [Fact]
    public void McpResult_Success_HasValueAndNoError()
    {
        var result = McpResult<string>.Success("ok");
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void McpResult_Failure_HasErrorAndNoValue()
    {
        var error = new McpError(McpErrorCode.ValidationFailed, "Invalid request", [], "test-1");
        var result = McpResult<string>.Failure(error);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
    }
}
