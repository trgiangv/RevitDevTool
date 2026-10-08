using System.Text.Json;
using DevTools.Daemon.Mcp.Code;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Ipc;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class CatalogScriptTests
{
    [TestMethod]
    public async Task Program_ProjectsHostResults()
    {
        var calls = new List<string>();
        var (runtime, _) = RuntimeWith(
            processId: 7,
            toolName: "query_equipment",
            description: "mechanical equipment in the model",
            onCall: name =>
            {
                calls.Add(name);
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "host-payload" }],
                    IsError = false,
                };
            });

        const string code = """
            var matched = await SearchAsync("mechanical equipment");
            var primitive = matched[0];
            var declaration = await DescribeAsync(primitive.Name, primitive.ProcessId);
            var first = await InvokeAsync(primitive.Name, new { category = "Mechanical Equipment" }, primitive.ProcessId);
            var second = await InvokeAsync(primitive.Name, new { category = "Mechanical Equipment" }, primitive.ProcessId);
            return new { tool = declaration.Name, text = ((TextContentBlock)first.Content[0]).Text, again = second.IsError == false };
            """;

        using var program = CodeModeCompiler.Compile(code);
        var value = await program.RunAsync(runtime);
        var result = CodeModeResult.ToCallToolResult(value);

        Assert.HasCount(2, calls);
        Assert.IsNull(result.StructuredContent);
        using var json = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
        Assert.AreEqual("query_equipment", json.RootElement.GetProperty("tool").GetString());
        Assert.AreEqual("host-payload", json.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task ReadOnly_RejectsAToolWithoutTheHint()
    {
        var (runtime, _) = RuntimeWith(7, "query_equipment", "mechanical equipment", _ => new CallToolResult(), readOnly: true);

        using var program = CodeModeCompiler.Compile("""return await InvokeAsync("query_equipment", null, 7);""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => program.RunAsync(runtime));
    }

    [TestMethod]
    public async Task OneProcess_DoesNotOverlap()
    {
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (runtime, _) = RuntimeWith(7, "ping", "ping tool", async _ =>
        {
            var n = Interlocked.Increment(ref entered);
            if (n == 1)
                await release.Task;
            return new CallToolResult { Content = [new TextContentBlock { Text = "ok" }] };
        });

        using var program = CodeModeCompiler.Compile("""
            await Task.WhenAll(InvokeAsync("ping", null, 7), InvokeAsync("ping", null, 7));
            return entered;
            """.Replace("entered", "1"));

        var running = program.RunAsync(runtime);
        var started = await WaitUntilAsync(() => Volatile.Read(ref entered) >= 1);
        Assert.IsTrue(started);
        await Task.Delay(50);
        Assert.AreEqual(1, Volatile.Read(ref entered));
        release.TrySetResult();
        await running;
        Assert.AreEqual(2, Volatile.Read(ref entered));
    }

    [TestMethod]
    public async Task TwoProcesses_Overlap()
    {
        var entered = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CallToolResult Ok() => new() { Content = [new TextContentBlock { Text = "ok" }] };
        var catalogs = new ProcessCatalogs();
        catalogs.Replace(Catalog(1, "ping", "ping one"));
        catalogs.Replace(Catalog(2, "ping", "ping two"));
        var sessions = new Dictionary<int, IProcessSession>
        {
            [1] = Session(async _ =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                    both.TrySetResult();
                await both.Task.WaitAsync(TimeSpan.FromSeconds(2));
                return Ok();
            }),
            [2] = Session(async _ =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                    both.TrySetResult();
                await both.Task.WaitAsync(TimeSpan.FromSeconds(2));
                return Ok();
            }),
        };
        var runtime = new CatalogRuntime(catalogs, id => sessions.GetValueOrDefault(id), readOnly: false);

        using var program = CodeModeCompiler.Compile("""
            await Task.WhenAll(InvokeAsync("ping", null, 1), InvokeAsync("ping", null, 2));
            return 1;
            """);

        await program.RunAsync(runtime).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(2, Volatile.Read(ref entered));
    }

    [TestMethod]
    public async Task ThrownBody_KeepsTheFirstInvoke()
    {
        var calls = 0;
        var (runtime, _) = RuntimeWith(7, "ping", "ping tool", name =>
        {
            var n = Interlocked.Increment(ref calls);
            if (n == 2)
                throw new InvalidOperationException("second failed");
            return new CallToolResult { Content = [new TextContentBlock { Text = name }] };
        });

        using var program = CodeModeCompiler.Compile("""
            await InvokeAsync("ping", null, 7);
            await InvokeAsync("ping", null, 7);
            return 1;
            """);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => program.RunAsync(runtime));
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void Cache_ReusesIdenticalSourceOnly()
    {
        var cache = new CodeModeCache();

        Assert.IsTrue(cache.SharesImage("return 1;", "return 1;"));
        Assert.IsFalse(cache.SharesImage("return 1;", "return 2;"));
    }

    private static (CatalogRuntime Runtime, ProcessCatalogs Catalogs) RuntimeWith(
        int processId,
        string toolName,
        string description,
        Func<string, CallToolResult> onCall,
        bool readOnly = false) =>
        RuntimeWith(processId, toolName, description, name => Task.FromResult(onCall(name)), readOnly);

    private static (CatalogRuntime Runtime, ProcessCatalogs Catalogs) RuntimeWith(
        int processId,
        string toolName,
        string description,
        Func<string, Task<CallToolResult>> onCall,
        bool readOnly = false)
    {
        var catalogs = new ProcessCatalogs();
        catalogs.Replace(Catalog(processId, toolName, description));
        var session = Session(onCall);
        var runtime = new CatalogRuntime(catalogs, id => id == processId ? session : null, readOnly);
        return (runtime, catalogs);
    }

    private static IProcessSession Session(Func<string, Task<CallToolResult>> onCall)
    {
        var session = new Mock<IProcessSession>();
        session
            .Setup(item => item.CallToolPassthroughAsync(It.IsAny<CallToolRequestParams>(), It.IsAny<CancellationToken>()))
            .Returns<CallToolRequestParams, CancellationToken>((parameters, _) =>
                onCall(parameters.Name!).ContinueWith(task => (Result)task.GetAwaiter().GetResult()));
        return session.Object;
    }

    private static ProcessCatalog Catalog(int processId, string toolName, string description) => new()
    {
        ProcessId = processId,
        Instance = new InstanceInfo { HostApp = "Revit", ProcessId = processId, VersionNumber = "2025" },
        PipeName = HostPipeName.FormatMcp("Revit", "2025", processId),
        Tools =
        [
            new Tool
            {
                Name = toolName,
                Description = description,
                InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new { category = new { type = "string" } },
                }),
            },
        ],
        Resources = [],
        ResourceTemplates = [],
    };

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50; i++)
        {
            if (condition())
                return true;
            await Task.Delay(20);
        }

        return condition();
    }
}
