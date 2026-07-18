using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public class ContractTests
{
    [Theory]
    [InlineData("McpBridgeMethods")]
    [InlineData("IpcPropertyNames")]
    [InlineData("McpPropertyNames")]
    [InlineData("McpBridgeRequestHandler")]
    public void RemovedMcpBridgeSymbols_DoNotAppearInProductionSource(string symbol)
    {
        var root = FindRepositoryRoot();
        var matches = Directory.EnumerateFiles(Path.Combine(root, "source"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(symbol, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(matches);
    }

    [Fact]
    public void BridgeMessage_RemainsOnlyInDirectPytestLane()
    {
        var root = FindRepositoryRoot();
        var matches = Directory.EnumerateFiles(Path.Combine(root, "source"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("BridgeMessage", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToArray();

        Assert.All(matches, path => Assert.True(
            path.StartsWith("source/DevTools.Ipc/", StringComparison.Ordinal) ||
            path.StartsWith("source/DevTools.Execution/External/Handlers/Pytest", StringComparison.Ordinal) ||
            path.Contains("DevToolsPipeServer", StringComparison.Ordinal), path));
    }

    [Fact]
    public void McpRegistryCatalog_DefaultsAreEmpty()
    {
        var catalog = new McpRegistryCatalog();
        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Prompts);
        Assert.Empty(catalog.Resources);

        Assert.Same(McpRegistryCatalog.Empty, McpRegistryCatalog.Empty);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_NormalizesDisplayNameAndToolId()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId("Read Walls", "Tools/Wall Tools");
        Assert.Equal("Read-Walls_[Tools/Wall-Tools]", id);

        var idWithSpaces = McpPrimitiveBinding.CreatePrimitiveId("read_walls", "sample:read_walls");
        Assert.Equal("read_walls_[sample:read_walls]", idWithSpaces);
    }

    [Fact]
    public void McpToolExecutionResultFactories_CreateExpectedShapes()
    {
        var completed = McpToolExecutionResult.Completed(
            new CallToolResult { Content = [new TextContentBlock { Text = "{\"answer\":42}" }] },
            "done");
        var failed = McpToolExecutionResult.Failed("tool.failed", "boom", "trace");
        var cancelled = McpToolExecutionResult.Cancelled("cancelled");

        Assert.Equal(ExecutionState.Completed, completed.State);
        Assert.NotNull(completed.Result);
        Assert.Single(completed.Result.Content);

        Assert.Equal(ExecutionState.Failed, failed.State);
        Assert.NotNull(failed.Error);
        Assert.Equal("tool.failed", failed.Error!.Code);
        Assert.Equal("boom", failed.Error.Message);
        Assert.Equal("trace", failed.Error.Details);

        Assert.Equal(ExecutionState.Cancelled, cancelled.State);
        Assert.NotNull(cancelled.Error);
        Assert.Equal("tool.cancelled", cancelled.Error!.Code);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_ForPromptsAndResources()
    {
        var promptId = McpPrimitiveBinding.CreatePrimitiveId("summarize_demo", "sample.dll:McpToolsetDemo.McpSamplePrompts.SummarizeDemo");
        var resourceId = McpPrimitiveBinding.CreatePrimitiveId("demo_view", "sample.dll:McpToolsetDemo.McpSampleResources.DemoView");

        Assert.Equal("summarize_demo_[sample.dll:McpToolsetDemo.McpSamplePrompts.SummarizeDemo]", promptId);
        Assert.Equal("demo_view_[sample.dll:McpToolsetDemo.McpSampleResources.DemoView]", resourceId);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_HandlesNullAndEmpty()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId(null, null);
        Assert.Equal("unknown_[unknown]", id);

        var idWithName = McpPrimitiveBinding.CreatePrimitiveId("tool", null);
        Assert.Equal("tool_[unknown]", idWithName);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
