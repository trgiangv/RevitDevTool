using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Hosting;
using DevTools.Mcp.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class McpCallFilterTests
{
    [TestMethod]
    public void ToToolError_InputRequired_IsAnErrorResult()
    {
        var result = McpCallFilter.ToToolError(new InputRequiredException(requestState: "round1"));

        Assert.IsTrue(result.IsError);
        Assert.IsNotNull(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
    }

    [TestMethod]
    public void ToToolError_UsesTheExceptionMessage()
    {
        var result = McpCallFilter.ToToolError(new InvalidOperationException("backend failed"));

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("backend failed", Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text);
    }

    [TestMethod]
    public void UnsupportedTool_NamesTheMissingSource()
    {
        var result = McpServerCollections.UnsupportedTool(ExecutionMode.Dotnet);

        Assert.IsTrue(result.IsError);
        Assert.Contains(
            "Unsupported MCP tool source",
            Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text,
            StringComparison.Ordinal);
    }
}
