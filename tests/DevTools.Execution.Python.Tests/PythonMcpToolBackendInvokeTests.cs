using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonMcpToolBackendInvokeTests
{
    [TestMethod]
    public void WriteRequest_NullRequest_ReturnsEmptyObject()
    {
        Assert.AreEqual("{}", PythonMcpToolBackend.WriteRequest(null));
    }

    [TestMethod]
    public void WriteRequest_WithArgumentsOnly_SerializesArguments()
    {
        var request = new CallToolRequestParams
        {
            Name = "echo",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["message"] = JsonSerializer.SerializeToElement("hello"),
            },
        };

        var json = PythonMcpToolBackend.WriteRequest(request);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual("hello", document.RootElement.GetProperty("message").GetString());
    }

    [TestMethod]
    public void WriteRequest_WithInputResponses_IncludesProtocolKeys()
    {
        var request = new CallToolRequestParams
        {
            Name = "echo",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["x"] = JsonSerializer.SerializeToElement(1),
            },
            InputResponses = new Dictionary<string, InputResponse>
            {
                ["prompt"] = new InputResponse { RawValue = JsonSerializer.SerializeToElement("answer") },
            },
            RequestState = "state-token",
        };

        var json = PythonMcpToolBackend.WriteRequest(request);
        using var document = JsonDocument.Parse(json);

        Assert.IsTrue(document.RootElement.TryGetProperty(McpSpecKeys.Tools.Arguments, out _));
        Assert.IsTrue(document.RootElement.TryGetProperty(McpSpecKeys.Tools.InputResponses, out _));
        Assert.AreEqual("state-token", document.RootElement.GetProperty(McpSpecKeys.Tools.RequestState).GetString());
    }

    [TestClass]
    public sealed class InvokeWithPythonTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task InvokeToolAsync_MissingSourcePath_ThrowsThroughHostContext()
        {
            var backend = new PythonMcpToolBackend(ExecutionTestHelpers.CreatePythonInitializer());
            var tool = new McpRegisteredTool
            {
                Id = "tool-1",
                Descriptor = new Tool { Name = "sample" },
                Binding = McpPrimitiveBinding.Create(ExecutionMode.Python, string.Empty, "mod", "run"),
            };

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => backend.InvokeToolAsync(
                    tool,
                    new CallToolRequestParams { Name = "sample" },
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task InvokeToolAsync_ExecutesMcpToolScriptAndReturnsSuccess()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var toolDir = ExecutionTestHelpers.CreateTempDirectory("python-mcp-tool");
            var toolPath = Path.Combine(toolDir, "echo_mcp.py");
            File.WriteAllText(toolPath, """
                from mcp.server.mcpserver import MCPServer

                mcp = MCPServer("echo-toolset")

                @mcp.tool()
                def echo(message: str = "hello") -> str:
                    return message
                """);

            var backend = new PythonMcpToolBackend(initializer);
            var tool = new McpRegisteredTool
            {
                Id = "tool-echo",
                Descriptor = new Tool { Name = "echo" },
                Binding = McpPrimitiveBinding.Create(ExecutionMode.Python, toolPath, "echo_mcp", "echo"),
            };

            try
            {
                var result = await backend.InvokeToolAsync(
                    tool,
                    new CallToolRequestParams
                    {
                        Name = "echo",
                        Arguments = new Dictionary<string, JsonElement>
                        {
                            ["message"] = JsonSerializer.SerializeToElement("world"),
                        },
                    },
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken);

                Assert.IsTrue(result.IsSuccess);
                Assert.IsNotNull(result.Value);
            }
            finally
            {
                TryDeleteDirectory(toolDir);
            }
        }

        [TestMethod]
        public async Task ReadResource_ExecutesMcpResourceScriptAndReturnsTextContents()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var toolDir = ExecutionTestHelpers.CreateTempDirectory("python-mcp-resource");
            var toolPath = Path.Combine(toolDir, "resource_mcp.py");
            File.WriteAllText(toolPath, """
                from mcp.server.mcpserver import MCPServer

                mcp = MCPServer("resource-toolset")

                @mcp.resource("test://item")
                def item_resource() -> str:
                    return "resource-body"
                """);

            var backend = new PythonMcpToolBackend(initializer);
            var resource = new McpRegisteredResource
            {
                Id = "res-1",
                Descriptor = new Resource { Uri = "test://item", Name = "item" },
                Binding = McpPrimitiveBinding.Create(ExecutionMode.Python, toolPath, "resource_mcp", "item_resource"),
            };

            try
            {
                var result = backend.ReadResource(resource, "test://item", TestContext.CancellationToken);
                Assert.AreEqual(1, result.Contents.Count);
                var text = (TextResourceContents)result.Contents[0];
                Assert.IsInstanceOfType(text, typeof(TextResourceContents));
                Assert.AreEqual("resource-body", text.Text);
            }
            finally
            {
                TryDeleteDirectory(toolDir);
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
