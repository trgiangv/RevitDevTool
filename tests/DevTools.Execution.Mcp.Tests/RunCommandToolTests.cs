using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using DevTools.Ipc;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using System.Text.Json;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class RunCommandToolTests
{
    private const string Entry = "My.Ns.Command";

    private readonly Mock<ICommandDiscovery> _discovery = new();
    private readonly Mock<ICommandRunner> _runner = new();
    private readonly Mock<IHostContextExecutor> _hostContext = new();
    private readonly Mock<IScriptExecutionStrategyFactory> _scriptFactory = new();
    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = ExecutionTestHelpers.CreateTempDirectory("run-command-tool");
        _hostContext
            .Setup(h => h.ExecuteAsync(It.IsAny<Func<ExecutionResult>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<ExecutionResult> handler, CancellationToken _) => Task.FromResult(handler()));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task RunCommand_EmptyPath_ReturnsErrorAndRunsNothing(string path)
    {
        var result = await InvokeToolAsync(new { path });

        Assert.IsTrue(result.IsError);
        Assert.Contains("path must not be empty", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task RunCommand_MissingFile_ReturnsErrorAndRunsNothing()
    {
        var missing = Path.Combine(_directory, "missing.dll");

        var result = await InvokeToolAsync(new { path = missing, entry = Entry });

        Assert.IsTrue(result.IsError);
        Assert.Contains("not found", Text(result));
        Assert.Contains(missing, Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task RunCommand_UnsupportedExtension_ReturnsErrorAndRunsNothing()
    {
        var path = await CreateFileAsync("notes.txt");

        var result = await InvokeToolAsync(new { path, entry = Entry });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Unsupported file type", Text(result));
        Assert.Contains(path, Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task RunCommand_DllWithoutEntry_ReturnsErrorAndRunsNothing()
    {
        var path = await CreateFileAsync("Commands.dll");

        var result = await InvokeToolAsync(new { path });

        Assert.IsTrue(result.IsError);
        Assert.Contains("entry is required", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    [DataRow("My.Ns.Other")]
    [DataRow("my.ns.command")]
    [DataRow("Command")]
    public async Task RunCommand_DllWithUnknownEntry_ReturnsErrorAndRunsNothing(string entry)
    {
        var path = await CreateFileAsync("Commands.dll");
        _discovery.Setup(d => d.ParseCommands(path)).Returns([new CommandItem(path, Entry)]);

        var result = await InvokeToolAsync(new { path, entry });

        Assert.IsTrue(result.IsError);
        var text = Text(result);
        Assert.Contains(entry, text);
        Assert.Contains(path, text);
        Assert.DoesNotContain(Entry, text);
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task RunCommand_DllWithMatchingEntry_RunsCommandAndReturnsSuccessPayload()
    {
        var path = await CreateFileAsync("Commands.dll");
        var command = new CommandItem(path, Entry);
        _discovery.Setup(d => d.ParseCommands(path)).Returns([new CommandItem(path, "My.Ns.Other"), command]);
        _runner.Setup(r => r.RunCommand(command)).Returns(ExecutionResult.Succeeded("done", 42));

        var result = await InvokeToolAsync(new { path, entry = Entry });

        Assert.IsFalse(result.IsError ?? false);
        _runner.Verify(r => r.RunCommand(command), Times.Once);
        using var document = JsonDocument.Parse(Text(result));
        Assert.AreEqual("Succeeded", document.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("done", document.RootElement.GetProperty(IpcPropertyNames.Message).GetString());
        Assert.AreEqual(42, document.RootElement.GetProperty("elapsedMs").GetInt64());
    }

    [TestMethod]
    public async Task RunCommand_RunnerFailure_ReturnsRuntimeErrorWithException()
    {
        var path = await CreateFileAsync("Commands.dll");
        var command = new CommandItem(path, Entry);
        _discovery.Setup(d => d.ParseCommands(path)).Returns([command]);
        _runner
            .Setup(r => r.RunCommand(command))
            .Returns(ExecutionResult.Failed("boom", new InvalidOperationException("bad state"), 7));

        var result = await InvokeToolAsync(new { path, entry = Entry });

        Assert.IsTrue(result.IsError);
        var text = Text(result);
        Assert.StartsWith(McpSpecKeys.Result.Runtime, text);
        using var document = JsonDocument.Parse(text[McpSpecKeys.Result.Runtime.Length..]);
        Assert.AreEqual("Failed", document.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("boom", document.RootElement.GetProperty(IpcPropertyNames.Message).GetString());
        Assert.AreEqual(typeof(InvalidOperationException).FullName, document.RootElement.GetProperty("exceptionType").GetString());
        Assert.AreEqual("bad state", document.RootElement.GetProperty("exceptionMessage").GetString());
    }

    [TestMethod]
    [DataRow("tool_script.py", ExecutionMode.Python)]
    [DataRow("tool_ipy_script.py", ExecutionMode.IronPython)]
    [DataRow("tool_script.fsx", ExecutionMode.FSharp)]
    [DataRow("tool_script.csx", ExecutionMode.CSharp)]
    [DataRow("sub/tool_script.py", ExecutionMode.Python)]
    public async Task RunCommand_FolderWithRelativeEntry_UsesModeAndFolderAsRoot(string entry, ExecutionMode mode)
    {
        var scriptPath = await CreateFileAsync(entry);
        var strategy = new Mock<IExecutionStrategy>();
        strategy
            .Setup(s => s.ExecuteAsync(It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.Succeeded("ok", 3));
        _scriptFactory.Setup(f => f.Create(mode, scriptPath, _directory)).Returns(strategy.Object);

        var result = await InvokeToolAsync(new { path = _directory, entry });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        _scriptFactory.Verify(f => f.Create(mode, scriptPath, _directory), Times.Once);
        _discovery.Verify(d => d.ParseCommands(It.IsAny<string>()), Times.Never);
        using var document = JsonDocument.Parse(Text(result));
        Assert.AreEqual("Succeeded", document.RootElement.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task RunCommand_FolderWithAbsoluteEntryInside_UsesFolderAsRoot()
    {
        var scriptPath = await CreateFileAsync("tool_script.py");
        _scriptFactory
            .Setup(f => f.Create(ExecutionMode.Python, scriptPath, _directory))
            .Returns(new StubStrategy());

        var result = await InvokeToolAsync(new { path = _directory, entry = scriptPath });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        _scriptFactory.Verify(f => f.Create(ExecutionMode.Python, scriptPath, _directory), Times.Once);
    }

    [TestMethod]
    [DataRow("tool_script.py")]
    [DataRow("tool_script.fsx")]
    public async Task RunCommand_SingleScriptFile_ReturnsErrorAndRunsNothing(string fileName)
    {
        var path = await CreateFileAsync(fileName);

        var result = await InvokeToolAsync(new { path, entry = fileName });

        Assert.IsTrue(result.IsError);
        Assert.Contains("working folder", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task RunCommand_FolderWithEntryOutsideFolder_ReturnsErrorAndRunsNothing()
    {
        var outside = ExecutionTestHelpers.CreateTempDirectory("run-command-outside");
        try
        {
            var outsideScript = Path.Combine(outside, "tool_script.py");
            await File.WriteAllTextAsync(outsideScript, "stub", TestContext.CancellationToken);

            var absolute = await InvokeToolAsync(new { path = _directory, entry = outsideScript });
            var relative = await InvokeToolAsync(new
            {
                path = _directory,
                entry = Path.Combine("..", Path.GetFileName(outside), "tool_script.py")
            });

            Assert.IsTrue(absolute.IsError);
            Assert.Contains("not inside", Text(absolute));
            Assert.IsTrue(relative.IsError);
            Assert.Contains("not inside", Text(relative));
            VerifyNothingRan();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("helper.py")]
    [DataRow("notes.txt")]
    public async Task RunCommand_FolderWithNonEntryFile_ReturnsErrorAndRunsNothing(string entry)
    {
        await CreateFileAsync(entry);

        var result = await InvokeToolAsync(new { path = _directory, entry });

        Assert.IsTrue(result.IsError);
        Assert.Contains("not an entry script", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task RunCommand_FolderWithMissingEntry_ReturnsErrorAndRunsNothing()
    {
        var result = await InvokeToolAsync(new { path = _directory, entry = "absent_script.py" });

        Assert.IsTrue(result.IsError);
        Assert.Contains("not found", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task RunCommand_FolderWithoutEntry_ReturnsErrorAndRunsNothing()
    {
        await CreateFileAsync("tool_script.py");

        var result = await InvokeToolAsync(new { path = _directory });

        Assert.IsTrue(result.IsError);
        Assert.Contains("entry is required", Text(result));
        VerifyNothingRan();
    }

    private void VerifyNothingRan()
    {
        _runner.Verify(r => r.RunCommand(It.IsAny<CommandItem>()), Times.Never);
        _scriptFactory.Verify(
            f => f.Create(It.IsAny<ExecutionMode>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    private async Task<string> CreateFileAsync(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(_directory, fileName));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "stub", TestContext.CancellationToken);
        return path;
    }

    private async Task<CallToolResult> InvokeToolAsync(object args)
    {
        var tool = new RunCommandTool(_discovery.Object, _runner.Object, _hostContext.Object, _scriptFactory.Object);
        var argumentMap = JsonSerializer.SerializeToElement(args).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);

        return await tool.ServerTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                Mock.Of<McpServer>(),
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                new CallToolRequestParams
                {
                    Name = tool.Name,
                    Arguments = argumentMap,
                }),
            TestContext.CancellationToken);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;

    private sealed class StubStrategy : IExecutionStrategy
    {
        public Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExecutionResult.Succeeded("ok", 1));
    }
}
