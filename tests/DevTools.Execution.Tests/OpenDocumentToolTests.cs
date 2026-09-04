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

public sealed class OpenDocumentToolTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OpenDocument_EmptyFilePath_ReturnsError(string filePath)
    {
        var bridge = new Mock<IDocumentBridge>();
        var tool = new OpenDocumentTool(bridge.Object);

        var result = await InvokeToolAsync(tool, new { filePath });

        Assert.True(result.IsError);
        Assert.Contains("filePath must not be empty", Text(result));
        bridge.Verify(
            b => b.OpenDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenDocument_MissingFile_ReturnsError()
    {
        var bridge = new Mock<IDocumentBridge>();
        var tool = new OpenDocumentTool(bridge.Object);
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.rvt");

        var result = await InvokeToolAsync(tool, new { filePath = missingPath });

        Assert.True(result.IsError);
        Assert.Contains("File not found", Text(result));
        bridge.Verify(
            b => b.OpenDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenDocument_ExistingFileAndBridgeSuccess_ReturnsSuccessPayload()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"open-doc-{Guid.NewGuid():N}.rvt");
        await File.WriteAllTextAsync(tempFile, "stub", TestContext.Current.CancellationToken);

        try
        {
            var bridge = new Mock<IDocumentBridge>();
            bridge
                .Setup(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DocumentOperationResult(true, "Opened", "Project1"));

            var tool = new OpenDocumentTool(bridge.Object);
            var result = await InvokeToolAsync(tool, new { filePath = tempFile });

            Assert.False(result.IsError ?? false);
            using var document = JsonDocument.Parse(Text(result));
            Assert.True(document.RootElement.GetProperty(IpcPropertyNames.Success).GetBoolean());
            Assert.Equal("Opened", document.RootElement.GetProperty(IpcPropertyNames.Message).GetString());
            Assert.Equal("Project1", document.RootElement.GetProperty("documentTitle").GetString());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task OpenDocument_BridgeFailure_ReturnsErrorPayload()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"open-doc-{Guid.NewGuid():N}.rvt");
        await File.WriteAllTextAsync(tempFile, "stub", TestContext.Current.CancellationToken);

        try
        {
            var bridge = new Mock<IDocumentBridge>();
            bridge
                .Setup(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DocumentOperationResult(false, "Host rejected file", null));

            var tool = new OpenDocumentTool(bridge.Object);
            var result = await InvokeToolAsync(tool, new { filePath = tempFile });

            Assert.True(result.IsError);
            using var document = JsonDocument.Parse(Text(result));
            Assert.False(document.RootElement.GetProperty(IpcPropertyNames.Success).GetBoolean());
            Assert.Equal("Host rejected file", document.RootElement.GetProperty(IpcPropertyNames.Message).GetString());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static async Task<CallToolResult> InvokeToolAsync(OpenDocumentTool tool, object args)
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
            TestContext.Current.CancellationToken);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;
}
