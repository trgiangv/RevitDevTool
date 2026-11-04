using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.Dotnet;
using DevTools.Execution.Services;
using DevTools.Ipc;
using DevTools.Mcp.Core.Protocol;
using DevTools.Settings.Configs;
using DevTools.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly Mock<IScriptExecutionStrategyFactory> _scriptFactory = new();
    private readonly List<(ExecutionMode Mode, string Script, string Root)> _scriptRuns = [];
    private readonly ExecutionConfig _config = new();
    private ExecutionOrchestrator _orchestrator = null!;
    private RunCommandTool _tool = null!;
    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = ExecutionTestHelpers.CreateTempDirectory("run-command-tool");
        _discovery.Setup(d => d.ParseCommands(It.IsAny<string>())).Returns([]);
        _scriptFactory
            .Setup(f => f.Create(It.IsAny<ExecutionMode>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((ExecutionMode mode, string script, string root) => new RecordingStrategy(() => _scriptRuns.Add((mode, script, root))));

        var hostContext = ExecutionTestHelpers.MockHostContext().Object;
        var assemblyProvider = new AssemblyExecutionProvider(
            _discovery.Object, hostContext, _runner.Object, NullLogger<AssemblyExecutionProvider>.Instance);
        var scriptProvider = new ScriptExecutionProvider(_scriptFactory.Object, NullLogger<ScriptExecutionProvider>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IExecutionProvider>(assemblyProvider);
        services.AddSingleton<IExecutionProvider>(scriptProvider);
        services.AddKeyedSingleton<IExecutionProvider>(ContainerMode.Assembly, assemblyProvider);
        services.AddKeyedSingleton<IExecutionProvider>(ContainerMode.Script, scriptProvider);
        var serviceProvider = services.BuildServiceProvider();

        _orchestrator = new ExecutionOrchestrator(
            serviceProvider,
            new TreeStateManager(),
            ExecutionTestHelpers.CreateFileWatcher(),
            ExecutionTestHelpers.CreateSettings(_config),
            new NoOpTelemetry(),
            NullLogger<ExecutionOrchestrator>.Instance);
        _tool = new RunCommandTool(_orchestrator);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _orchestrator.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Run_EmptyPath_ReturnsErrorAndRunsNothing(string path)
    {
        var result = await InvokeToolAsync(new { path });

        Assert.IsTrue(result.IsError);
        Assert.Contains("path must not be empty", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Run_MissingFile_ReturnsErrorAndRunsNothing()
    {
        var missing = Path.Combine(_directory, "missing.dll");

        var result = await InvokeToolAsync(new { path = missing, entry = Entry });

        Assert.IsTrue(result.IsError);
        Assert.Contains("not found", Text(result));
        Assert.Contains(missing, Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Run_UnsupportedExtension_ReturnsErrorAndRunsNothing()
    {
        var path = await CreateFileAsync("notes.txt");

        var result = await InvokeToolAsync(new { path, entry = Entry });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Unsupported file type", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Run_DllWithoutEntry_ReturnsErrorAndRunsNothing()
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
    public async Task Run_DllWithUnknownEntry_ReturnsErrorAndRunsNothing(string entry)
    {
        var path = await CreateDllAsync(Entry);

        var result = await InvokeToolAsync(new { path, entry });

        Assert.IsTrue(result.IsError);
        var text = Text(result);
        Assert.Contains(entry, text);
        Assert.Contains(path, text);
        Assert.DoesNotContain(Entry, text);
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Run_DllWithMatchingEntry_RunsCommandAndReturnsSuccessPayload()
    {
        var path = await CreateDllAsync("My.Ns.Other", Entry);
        _runner.Setup(r => r.RunCommand(It.IsAny<CommandItem>())).Returns(ExecutionResult.Succeeded("done", 42));

        var result = await InvokeToolAsync(new { path, entry = Entry });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        _runner.Verify(r => r.RunCommand(It.Is<CommandItem>(c => c.FullClassName == Entry)), Times.Once);
        using var document = JsonDocument.Parse(Text(result));
        Assert.AreEqual("Succeeded", document.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("done", document.RootElement.GetProperty(IpcPropertyNames.Message).GetString());
        Assert.AreEqual(42, document.RootElement.GetProperty("elapsedMs").GetInt64());
    }

    [TestMethod]
    public async Task Run_Dll_AddsRootToTreeAndSettings()
    {
        var path = await CreateDllAsync(Entry);
        _runner.Setup(r => r.RunCommand(It.IsAny<CommandItem>())).Returns(ExecutionResult.Succeeded("done", 1));

        await InvokeToolAsync(new { path, entry = Entry });
        await InvokeToolAsync(new { path, entry = Entry });

        Assert.AreEqual(path, _orchestrator.FindRoot(path)?.RootPath);
        CollectionAssert.AreEqual(new[] { path }, _config.DotnetAssemblyPaths);
        _discovery.Verify(d => d.ParseCommands(path), Times.Once);
    }

    [TestMethod]
    public async Task Run_EntryAddedAfterRebuild_ReloadsRootAndRuns()
    {
        var path = await CreateDllAsync("My.Ns.Other");
        _runner.Setup(r => r.RunCommand(It.IsAny<CommandItem>())).Returns(ExecutionResult.Succeeded("done", 1));
        await InvokeToolAsync(new { action = "add", path });
        _discovery.Setup(d => d.ParseCommands(path)).Returns([new CommandItem(path, "My.Ns.Other"), new CommandItem(path, Entry)]);

        var result = await InvokeToolAsync(new { path, entry = Entry });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        _runner.Verify(r => r.RunCommand(It.Is<CommandItem>(c => c.FullClassName == Entry)), Times.Once);
    }

    [TestMethod]
    public async Task Run_RunnerFailure_ReturnsRuntimeErrorWithException()
    {
        var path = await CreateDllAsync(Entry);
        _runner
            .Setup(r => r.RunCommand(It.IsAny<CommandItem>()))
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
    public async Task Run_FolderWithRelativeEntry_UsesModeAndFolderAsRoot(string entry, ExecutionMode mode)
    {
        var scriptPath = await CreateFileAsync(entry);

        var result = await InvokeToolAsync(new { path = _directory, entry });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        Assert.AreEqual((mode, scriptPath, _directory), _scriptRuns.Single());
        _discovery.Verify(d => d.ParseCommands(It.IsAny<string>()), Times.Never);
        CollectionAssert.AreEqual(new[] { _directory }, _config.ScriptFolderPaths);
    }

    [TestMethod]
    public async Task Run_FolderWithAbsoluteEntryInside_UsesFolderAsRoot()
    {
        var scriptPath = await CreateFileAsync("tool_script.py");

        var result = await InvokeToolAsync(new { path = _directory, entry = scriptPath });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        Assert.AreEqual((ExecutionMode.Python, scriptPath, _directory), _scriptRuns.Single());
    }

    [TestMethod]
    public async Task Run_SubfolderOfLoadedRoot_RunsThroughParentRoot()
    {
        var scriptPath = await CreateFileAsync("sub/tool_script.py");
        await InvokeToolAsync(new { action = "add", path = _directory });

        var result = await InvokeToolAsync(new { path = Path.Combine(_directory, "sub"), entry = "tool_script.py" });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        Assert.AreEqual((ExecutionMode.Python, scriptPath, _directory), _scriptRuns.Single());
        Assert.HasCount(1, _orchestrator.TreeRoot);
    }

    [TestMethod]
    [DataRow("tool_script.py")]
    [DataRow("tool_script.fsx")]
    public async Task Run_SingleScriptFile_ReturnsErrorAndRunsNothing(string fileName)
    {
        var path = await CreateFileAsync(fileName);

        var result = await InvokeToolAsync(new { path, entry = fileName });

        Assert.IsTrue(result.IsError);
        Assert.Contains("working folder", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Run_FolderWithEntryOutsideFolder_ReturnsErrorAndRunsNothing()
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
    public async Task Run_FolderWithNonEntryFile_ReturnsErrorAndRunsNothing(string entry)
    {
        await CreateFileAsync(entry);

        var result = await InvokeToolAsync(new { path = _directory, entry });

        Assert.IsTrue(result.IsError);
        Assert.Contains("not an entry script", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Run_FolderWithMissingEntry_ReturnsErrorAndRunsNothing()
    {
        var result = await InvokeToolAsync(new { path = _directory, entry = "absent_script.py" });

        Assert.IsTrue(result.IsError);
        Assert.Contains("not found", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Run_FolderWithoutEntry_ReturnsErrorAndRunsNothing()
    {
        await CreateFileAsync("tool_script.py");

        var result = await InvokeToolAsync(new { path = _directory });

        Assert.IsTrue(result.IsError);
        Assert.Contains("entry is required", Text(result));
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Add_NewAndRepeatedPath_ReportsAddedThenAlreadyLoaded()
    {
        var path = await CreateDllAsync(Entry);

        var first = await InvokeToolAsync(new { action = "add", path });
        var second = await InvokeToolAsync(new { action = "add", path });

        using var firstDocument = JsonDocument.Parse(Text(first));
        Assert.AreEqual("Added", firstDocument.RootElement.GetProperty("status").GetString());
        var root = firstDocument.RootElement.GetProperty("root");
        Assert.AreEqual("assembly", root.GetProperty("kind").GetString());
        Assert.AreEqual(Entry, root.GetProperty("entries")[0].GetString());
        using var secondDocument = JsonDocument.Parse(Text(second));
        Assert.AreEqual("AlreadyLoaded", secondDocument.RootElement.GetProperty("status").GetString());
        CollectionAssert.AreEqual(new[] { path }, _config.DotnetAssemblyPaths);
        VerifyNothingRan();
    }

    [TestMethod]
    public async Task Add_FolderWithoutEntryScripts_ReturnsError()
    {
        await CreateFileAsync("helper.py");

        var result = await InvokeToolAsync(new { action = "add", path = _directory });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Nothing to load", Text(result));
        Assert.IsEmpty(_config.ScriptFolderPaths);
    }

    [TestMethod]
    public async Task Remove_LoadedRoot_DropsItFromTreeAndSettings()
    {
        await CreateFileAsync("tool_script.py");
        await InvokeToolAsync(new { action = "add", path = _directory });

        var result = await InvokeToolAsync(new { action = "remove", path = _directory });

        Assert.IsFalse(result.IsError ?? false, Text(result));
        Assert.IsEmpty(_orchestrator.TreeRoot);
        Assert.IsEmpty(_config.ScriptFolderPaths);
    }

    [TestMethod]
    public async Task Remove_PathThatIsNotARoot_ReturnsError()
    {
        await CreateFileAsync("sub/tool_script.py");
        await InvokeToolAsync(new { action = "add", path = _directory });

        var result = await InvokeToolAsync(new { action = "remove", path = Path.Combine(_directory, "sub") });

        Assert.IsTrue(result.IsError);
        Assert.Contains("is not a root", Text(result));
        Assert.HasCount(1, _orchestrator.TreeRoot);
    }

    [TestMethod]
    public async Task List_ReturnsSavedAndAddedRootsWithRunnableEntries()
    {
        var dll = await CreateDllAsync(Entry);
        _config.DotnetAssemblyPaths.Add(dll);
        await CreateFileAsync("sub/tool_script.py");
        await InvokeToolAsync(new { action = "add", path = _directory });

        var all = await InvokeToolAsync(new { action = "list" });
        var one = await InvokeToolAsync(new { action = "list", path = Path.Combine(_directory, "sub") });

        using var allDocument = JsonDocument.Parse(Text(all));
        var roots = allDocument.RootElement.GetProperty("roots");
        Assert.AreEqual(2, roots.GetArrayLength());
        Assert.AreEqual(dll, roots[0].GetProperty("path").GetString());
        Assert.AreEqual(Entry, roots[0].GetProperty("entries")[0].GetString());
        Assert.AreEqual(Path.Combine("sub", "tool_script.py"), roots[1].GetProperty("entries")[0].GetString());
        using var oneDocument = JsonDocument.Parse(Text(one));
        Assert.AreEqual(_directory, oneDocument.RootElement.GetProperty("roots").EnumerateArray().Single().GetProperty("path").GetString());
    }

    [TestMethod]
    public async Task UnknownAction_ReturnsError()
    {
        var result = await InvokeToolAsync(new { action = "clear" });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Unknown action", Text(result));
    }

    private void VerifyNothingRan()
    {
        _runner.Verify(r => r.RunCommand(It.IsAny<CommandItem>()), Times.Never);
        Assert.IsEmpty(_scriptRuns);
    }

    private async Task<string> CreateDllAsync(params string[] commands)
    {
        var path = await CreateFileAsync("Commands.dll");
        _discovery.Setup(d => d.ParseCommands(path)).Returns(commands.Select(command => new CommandItem(path, command)).ToList());
        return path;
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
        var argumentMap = JsonSerializer.SerializeToElement(args).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);

        return await _tool.ServerTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                Mock.Of<McpServer>(),
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                new CallToolRequestParams
                {
                    Name = _tool.Name,
                    Arguments = argumentMap,
                }),
            TestContext.CancellationToken);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;

    private sealed class RecordingStrategy(Action onRun) : IExecutionStrategy
    {
        public Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            onRun();
            return Task.FromResult(ExecutionResult.Succeeded("ok", 1));
        }
    }
}
