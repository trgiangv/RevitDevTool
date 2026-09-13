using DevTools.Mcp.Core.Results;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpResultContractTests
{
    [TestMethod]
    public void McpResult_Success_HasValueAndNoError()
    {
        var result = McpResult<string>.Success("ok");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("ok", result.Value);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public void McpResult_Failure_HasErrorAndNoValue()
    {
        var error = new McpError(McpErrorCode.ValidationFailed, "Invalid request", [], "test-1");
        var result = McpResult<string>.Failure(error);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Value);
        Assert.AreEqual(error, result.Error);
    }
}
