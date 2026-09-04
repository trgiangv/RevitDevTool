using System.Diagnostics;
using System.IO.Pipelines;
using System.Text.Json;
using DevTools.FileMetadata.Core;
using DevTools.Hosting;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Hosting;
using DevTools.Mcp.Server.Prompts;
using DevTools.Mcp.Server.Tests.Harness;
using DevTools.Mcp.Server.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Server.Tests;

public sealed class LocalToolsAndPromptsTests
{
    [Fact]
    public void McpEngine_RegistersDaemonToolsAndPrompts()
    {
        var broker = new Mock<IHostBroker>();
        broker.Setup(b => b.Catalog).Returns(new ConnectedHostCatalog());
        var engine = new McpEngine(
            broker.Object,
            Mock.Of<IMcpPipeScanner>(),
            Mock.Of<IHostLaunchService>(),
            Mock.Of<IMachineLister>(),
            Mock.Of<IFileReaderCatalog>());

        Assert.Equal(6, engine.LocalTools.Count);
        Assert.Equal(2, engine.PromptCollection.Count);
        Assert.Contains(engine.LocalTools, tool => tool.ProtocolTool.Name == "launch_host");
        Assert.Contains(engine.LocalTools, tool => tool.ProtocolTool.Name == "list_machines");
        Assert.Contains(engine.LocalTools, tool => tool.ProtocolTool.Name == "read_file_info");
    }

    [Fact]
    public async Task ListMachinesTool_ReturnsListerPayload()
    {
        var lister = new Mock<IMachineLister>();
        lister.Setup(l => l.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallToolResult { Content = [new TextContentBlock { Text = "machines" }] });

        var tool = ListMachinesTool.Create(lister.Object);
        var result = await McpToolInvoke.Invoke(tool, "list_machines", new { });

        Assert.Equal("machines", McpToolInvoke.Text(result));
    }

    [Fact]
    public async Task ReadFileInfoTool_ReadsSummaryAndHandlesErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcp-read-{Guid.NewGuid():N}.rvt");
        await File.WriteAllTextAsync(path, "demo", TestContext.Current.CancellationToken);

        var reader = new Mock<IFileReader>();
        reader.Setup(r => r.SupportedExtensions).Returns([".rvt"]);
        reader.Setup(r => r.Read(It.IsAny<FileInfoRequest>()))
            .Returns(new TestFileInfoResult { HostApplication = HostApp.Revit, FilePath = path, FileName = Path.GetFileName(path) });

        var catalog = new Mock<IFileReaderCatalog>();
        catalog.Setup(c => c.GetReader(path)).Returns(reader.Object);
        catalog.Setup(c => c.FormatSupportedExtensions()).Returns(".rvt");

        var tool = ReadFileInfoTool.Create(catalog.Object);

        var summary = await McpToolInvoke.Invoke(tool, "read_file_info", new { filePath = path });
        Assert.Equal(path, JsonDocument.Parse(McpToolInvoke.Text(summary)).RootElement.GetProperty("filePath").GetString());

        var full = await McpToolInvoke.Invoke(tool, "read_file_info", new { filePath = path, detail = "full" });
        Assert.Equal(path, JsonDocument.Parse(McpToolInvoke.Text(full)).RootElement.GetProperty("filePath").GetString());

        var missing = await McpToolInvoke.Invoke(tool, "read_file_info", new { filePath = path + ".missing" });
        Assert.Contains("File not found", McpToolInvoke.Text(missing), StringComparison.Ordinal);

        var empty = await McpToolInvoke.Invoke(tool, "read_file_info", new { filePath = " " });
        Assert.Contains("filePath is required", McpToolInvoke.Text(empty), StringComparison.Ordinal);

        catalog.Setup(c => c.GetReader(It.IsAny<string>()))
            .Throws(new FileReadException(FileError.UnsupportedFormat, "bad"));
        var unsupported = await McpToolInvoke.Invoke(tool, "read_file_info", new { filePath = path });
        Assert.Contains("Unsupported file extension", McpToolInvoke.Text(unsupported), StringComparison.Ordinal);

        catalog.Setup(c => c.GetReader(It.IsAny<string>())).Throws(new InvalidOperationException("boom"));
        var failed = await McpToolInvoke.Invoke(tool, "read_file_info", new { filePath = path });
        Assert.Contains("Failed to read file", McpToolInvoke.Text(failed), StringComparison.Ordinal);

        try { File.Delete(path); } catch { /* ignored */ }
    }

    [Fact]
    public async Task LaunchHostTool_ValidatesInputAndLaunchesWhenBridgeConnects()
    {
        var broker = new Mock<IHostBroker>();
        var session = new Mock<IHostSession>();
        broker.Setup(b => b.GetByProcessId(Environment.ProcessId)).Returns(session.Object);

        var launch = new Mock<IHostLaunchService>();
        launch.Setup(s => s.Start(It.IsAny<HostLaunchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(new HostProcessStart(
                Process.GetCurrentProcess(),
                "2025",
                @"C:\Revit\Revit.exe",
                "fr-FR",
                ["/language", "FRA"],
                null));

        var tool = LaunchHostTool.Create(broker.Object, launch.Object);

        var missing = await McpToolInvoke.Invoke(tool, "launch_host", new { });
        Assert.Contains("hostApp is required", McpToolInvoke.Text(missing), StringComparison.Ordinal);

        launch.Setup(s => s.Start(It.IsAny<HostLaunchRequest>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("already running"));
        var startError = await McpToolInvoke.Invoke(tool, "launch_host", new { hostApp = "Revit" });
        Assert.Contains("already running", McpToolInvoke.Text(startError), StringComparison.Ordinal);

        launch.Setup(s => s.Start(It.IsAny<HostLaunchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(new HostProcessStart(
                Process.GetCurrentProcess(),
                "2025",
                @"C:\Revit\Revit.exe",
                "fr-FR",
                ["/language", "FRA"],
                null));

        var success = await McpToolInvoke.Invoke(tool, "launch_host", new { hostApp = "Revit", languageCode = "fr-FR" });
        var payload = JsonSerializer.Deserialize<LaunchHostResult>(McpToolInvoke.Text(success), McpServerJsonContext.Default.LaunchHostResult);
        Assert.NotNull(payload);
        Assert.Equal(HostApp.Revit, payload.HostApp);
        Assert.True(payload.BridgeConnected);
        Assert.Equal("fr-FR", payload.LanguageCode);
    }

    [Fact]
    public async Task LaunchHostTool_InfersHostFromFilePath_AndReportsExitedProcess()
    {
        using var exited = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true, UseShellExecute = false });
        Assert.NotNull(exited);
        exited.WaitForExit();

        var broker = new Mock<IHostBroker>();
        var launch = new Mock<IHostLaunchService>();
        launch.Setup(s => s.Start(It.IsAny<HostLaunchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(new HostProcessStart(
                exited,
                "2025",
                @"C:\Revit\Revit.exe",
                null,
                [],
                null));

        var tool = LaunchHostTool.Create(broker.Object, launch.Object);
        var inferred = await McpToolInvoke.Invoke(tool, "launch_host", new { filePath = @"C:\models\demo.rvt" });
        Assert.Contains("Revit", McpToolInvoke.Text(inferred), StringComparison.Ordinal);

        var exitedResult = await McpToolInvoke.Invoke(tool, "launch_host", new { hostApp = "Revit" });
        Assert.Contains("exited before the bridge connected", McpToolInvoke.Text(exitedResult), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaunchHostTool_CancelledToken_PropagatesCancellation()
    {
        var broker = new Mock<IHostBroker>();
        var launch = new Mock<IHostLaunchService>();
        launch.Setup(s => s.Start(It.IsAny<HostLaunchRequest>(), It.IsAny<CancellationToken>()))
            .Returns(new HostProcessStart(
                Process.GetCurrentProcess(),
                "2025",
                @"C:\Revit\Revit.exe",
                null,
                [],
                null));
        broker.Setup(b => b.GetByProcessId(It.IsAny<int>())).Returns((IHostSession?)null);

        var tool = LaunchHostTool.Create(broker.Object, launch.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await tool.InvokeAsync(McpServerConfigurationTests.CreateToolRequest(tool), cts.Token));
    }

    [Fact]
    public async Task RevitCodePrompt_EmitsManualAndReadonlyTemplates()
    {
        await using var harness = await PromptHarness.StartAsync(RevitCodePrompt.Create());
        var manual = await harness.Client.GetPromptAsync(
            "revit_code",
            new Dictionary<string, object?> { ["task"] = "list walls" },
            cancellationToken: TestContext.Current.CancellationToken);
        var manualText = Assert.IsType<TextContentBlock>(manual.Messages[0].Content).Text;
        Assert.Contains("TransactionMode.Manual", manualText, StringComparison.Ordinal);
        Assert.Contains("list walls", manualText, StringComparison.Ordinal);

        var readOnly = await harness.Client.GetPromptAsync(
            "revit_code",
            new Dictionary<string, object?> { ["task"] = "count doors", ["mode"] = "readonly" },
            cancellationToken: TestContext.Current.CancellationToken);
        var readOnlyText = Assert.IsType<TextContentBlock>(readOnly.Messages[0].Content).Text;
        Assert.Contains("TransactionMode.ReadOnly", readOnlyText, StringComparison.Ordinal);
        Assert.Contains("Do NOT create a Transaction", readOnlyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcadCodePrompt_EmitsModifyAndReadonlyTemplates()
    {
        await using var harness = await PromptHarness.StartAsync(AcadCodePrompt.Create());
        var modify = await harness.Client.GetPromptAsync(
            "acad_code",
            new Dictionary<string, object?> { ["task"] = "draw line" },
            cancellationToken: TestContext.Current.CancellationToken);
        var modifyText = Assert.IsType<TextContentBlock>(modify.Messages[0].Content).Text;
        Assert.Contains("tr.Commit()", modifyText, StringComparison.Ordinal);

        var readOnly = await harness.Client.GetPromptAsync(
            "acad_code",
            new Dictionary<string, object?> { ["task"] = "list layers", ["mode"] = "readonly" },
            cancellationToken: TestContext.Current.CancellationToken);
        var readOnlyText = Assert.IsType<TextContentBlock>(readOnly.Messages[0].Content).Text;
        Assert.Contains("OpenMode.ForRead", readOnlyText, StringComparison.Ordinal);
        Assert.Contains("Do NOT call tr.Commit()", readOnlyText, StringComparison.Ordinal);
    }

    private sealed class PromptHarness : IAsyncDisposable
    {
        private readonly Pipe _clientToServer;
        private readonly Pipe _serverToClient;
        private readonly CancellationTokenSource _cts;
        private readonly Task _serverTask;
        private readonly McpServer _server;

        private PromptHarness(McpClient client, McpServer server, Task serverTask, Pipe clientToServer, Pipe serverToClient, CancellationTokenSource cts)
        {
            Client = client;
            _server = server;
            _serverTask = serverTask;
            _clientToServer = clientToServer;
            _serverToClient = serverToClient;
            _cts = cts;
        }

        public McpClient Client { get; }

        public static async Task<PromptHarness> StartAsync(McpServerPrompt prompt)
        {
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var cts = new CancellationTokenSource();
            var prompts = new McpServerPrimitiveCollection<McpServerPrompt>();
            prompts.TryAdd(prompt);

            var options = new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "prompt-host", Version = "1.0.0" },
                PromptCollection = prompts,
                Capabilities = new ServerCapabilities { Prompts = new PromptsCapability() },
            };

            var transport = new StreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream(),
                "prompt-server",
                NullLoggerFactory.Instance);
            var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, TestMcpAppServices.Create());
            var serverTask = server.RunAsync(cts.Token);

            var client = await McpClient.CreateAsync(
                new StreamClientTransport(
                    clientToServer.Writer.AsStream(),
                    serverToClient.Reader.AsStream(),
                    NullLoggerFactory.Instance),
                loggerFactory: NullLoggerFactory.Instance,
                cancellationToken: TestContext.Current.CancellationToken);

            return new PromptHarness(client, server, serverTask, clientToServer, serverToClient, cts);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _cts.CancelAsync();
            _clientToServer.Writer.Complete();
            _serverToClient.Writer.Complete();
            try { await _serverTask; } catch { /* ignored */ }
            await _server.DisposeAsync();
            _cts.Dispose();
        }
    }

    private sealed class TestFileInfoResult : FileInfoResult;
}
