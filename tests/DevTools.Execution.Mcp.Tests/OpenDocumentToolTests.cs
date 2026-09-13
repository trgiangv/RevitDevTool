using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Ipc;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class OpenDocumentToolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task OpenDocument_EmptyFilePath_ReturnsError(string filePath)
    {
        var bridge = new Mock<IDocumentBridge>();
        var tool = new OpenDocumentTool(bridge.Object);

        var result = await InvokeToolAsync(tool, new { filePath });

        Assert.IsTrue(result.IsError);
        Assert.Contains("filePath must not be empty", Text(result));
        bridge.Verify(
            b => b.OpenDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OpenDocument_MissingFile_ReturnsError()
    {
        var bridge = new Mock<IDocumentBridge>();
        var tool = new OpenDocumentTool(bridge.Object);
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.rvt");

        var result = await InvokeToolAsync(tool, new { filePath = missingPath });

        Assert.IsTrue(result.IsError);
        Assert.Contains("File not found", Text(result));
        bridge.Verify(
            b => b.OpenDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OpenDocument_ExistingFileAndBridgeSuccess_ReturnsSuccessPayload()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"open-doc-{Guid.NewGuid():N}.rvt");
        await File.WriteAllTextAsync(tempFile, "stub", TestContext.CancellationToken);

        try
        {
            var bridge = new Mock<IDocumentBridge>();
            bridge
                .Setup(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DocumentOperationResult(true, "Opened", "Project1"));

            var tool = new OpenDocumentTool(bridge.Object);
            var result = await InvokeToolAsync(tool, new { filePath = tempFile });

            Assert.IsFalse(result.IsError ?? false);
            using var document = JsonDocument.Parse(Text(result));
            Assert.IsTrue(document.RootElement.GetProperty(IpcPropertyNames.Success).GetBoolean());
            Assert.AreEqual("Opened", document.RootElement.GetProperty(IpcPropertyNames.Message).GetString());
            Assert.AreEqual("Project1", document.RootElement.GetProperty("documentTitle").GetString());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task OpenDocument_BridgeFailure_ReturnsErrorPayload()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"open-doc-{Guid.NewGuid():N}.rvt");
        await File.WriteAllTextAsync(tempFile, "stub", TestContext.CancellationToken);

        try
        {
            var bridge = new Mock<IDocumentBridge>();
            bridge
                .Setup(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DocumentOperationResult(false, "Host rejected file", null));

            var tool = new OpenDocumentTool(bridge.Object);
            var result = await InvokeToolAsync(tool, new { filePath = tempFile });

            Assert.IsTrue(result.IsError);
            using var document = JsonDocument.Parse(Text(result));
            Assert.IsFalse(document.RootElement.GetProperty(IpcPropertyNames.Success).GetBoolean());
            Assert.AreEqual("Host rejected file", document.RootElement.GetProperty(IpcPropertyNames.Message).GetString());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private async Task<CallToolResult> InvokeToolAsync(OpenDocumentTool tool, object args)
    {
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
}
