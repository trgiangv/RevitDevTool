#pragma warning disable MCPEXP001

using System.Text.Json;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class DaemonMcpTasksHeadlessTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CallTool_NoTaskOptIn_ReturnsInlineResultAndCreatesNoTask()
    {
        await using var harness = await DaemonMcpTaskHarness.StartAsync(
            TestContext.CancellationToken,
            new McpClientOptions { ProtocolVersion = "2025-11-25" });

        var augmented = await harness.Client.CallToolAsTaskAsync(
            Code(TaskTestProcessSessions.DefaultToolName),
            TestContext.CancellationToken);

        Assert.IsFalse(augmented.IsTask);
        Assert.IsNotNull(augmented.Result);
        Assert.Contains("called:task_demo_tool", DaemonMcpTaskHarness.Text(augmented.Result!), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CallTool_WithTaskOptIn_ReturnsCreateTaskResultAndPolledResultMatchesHost()
    {
        await using var harness = await DaemonMcpTaskHarness.StartAsync(TestContext.CancellationToken);
        var request = Code(TaskTestProcessSessions.DefaultToolName);
        var sync = await harness.Client.CallToolAsync(request, cancellationToken: TestContext.CancellationToken);
        var augmented = await harness.Client.CallToolAsTaskAsync(request, TestContext.CancellationToken);

        Assert.IsTrue(augmented.IsTask);
        Assert.IsNotNull(augmented.TaskCreated);
        Assert.AreEqual(McpTaskStatus.Working, augmented.TaskCreated!.Status);

        var completed = await DaemonMcpTaskHarness.PollCallToolResultAsync(
            harness.Client,
            augmented.TaskCreated.TaskId,
            TestContext.CancellationToken);

        Assert.AreEqual(DaemonMcpTaskHarness.Text(sync), DaemonMcpTaskHarness.Text(completed));
    }

    [TestMethod]
    public async Task TaskCancel_EndsCancelledAndSignalsHostToken()
    {
        await using var harness = await DaemonMcpTaskHarness.StartAsync(TestContext.CancellationToken);
        var augmented = await harness.Client.CallToolAsTaskAsync(Code(TaskTestProcessSessions.SlowToolName), TestContext.CancellationToken);
        Assert.IsTrue(augmented.IsTask);

        await harness.Sessions.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        await harness.Client.CancelTaskAsync(augmented.TaskCreated!.TaskId, TestContext.CancellationToken);

        var terminal = await DaemonMcpTaskHarness.PollTaskStateAsync(
            harness.Client,
            augmented.TaskCreated.TaskId,
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<CancelledTaskResult>(terminal);
        await harness.Sessions.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task InvokeDynamic_IsErrorHostResult_EndsCompleted()
    {
        await using var harness = await DaemonMcpTaskHarness.StartAsync(TestContext.CancellationToken);
        var augmented = await harness.Client.CallToolAsTaskAsync(Code(TaskTestProcessSessions.ErrorToolName), TestContext.CancellationToken);
        Assert.IsTrue(augmented.IsTask);

        var completed = await DaemonMcpTaskHarness.PollCallToolResultAsync(
            harness.Client,
            augmented.TaskCreated!.TaskId,
            TestContext.CancellationToken);

        Assert.IsTrue(completed.IsError);
        Assert.AreEqual("host tool failed", DaemonMcpTaskHarness.Text(completed));
    }

    [TestMethod]
    public async Task Tool_McpProtocolException_EndsFailed()
    {
        await using var harness = await DaemonMcpTaskHarness.StartAsync(
            TestContext.CancellationToken,
            includeProtocolFaultTool: true);

        var augmented = await harness.Client.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = DaemonTaskTestTools.ThrowsProtocolName },
            TestContext.CancellationToken);

        Assert.IsTrue(augmented.IsTask);

        var terminal = await DaemonMcpTaskHarness.PollTaskStateAsync(
            harness.Client,
            augmented.TaskCreated!.TaskId,
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<FailedTaskResult>(terminal);
    }

    [TestMethod]
    public void AddMcp_ConfiguresSharedTaskStoreDefaultsAndSelector()
    {
        var services = new ServiceCollection();
        services.AddMcp();

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IMcpTaskStore>();
        var memory = Assert.IsInstanceOfType<InMemoryMcpTaskStore>(store);

        Assert.AreEqual(TimeSpan.FromMinutes(30), memory.DefaultTimeToLive);
        Assert.AreEqual(1000, memory.DefaultPollIntervalMs);
        Assert.AreSame(store, provider.GetRequiredService<IMcpTaskStore>());
    }

    private static CallToolRequestParams Code(string toolName) => new()
    {
        Name = "code_mode",
        Arguments = new Dictionary<string, JsonElement>
        {
            ["code"] = JsonSerializer.SerializeToElement(
                $"return await InvokeAsync(\"{toolName}\", null, {TaskTestProcessSessions.DefaultProcessId});"),
        },
    };
}
