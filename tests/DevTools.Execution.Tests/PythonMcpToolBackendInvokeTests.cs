using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.Tests;

public sealed class PythonMcpToolBackendInvokeTests
{
    [Fact]
    public void WriteRequest_NullRequest_ReturnsEmptyObject()
    {
        Assert.Equal("{}", PythonMcpToolBackend.WriteRequest(null));
    }

    [Fact]
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
        Assert.Equal("hello", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
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

        Assert.True(document.RootElement.TryGetProperty(McpSpecKeys.Tools.Arguments, out _));
        Assert.True(document.RootElement.TryGetProperty(McpSpecKeys.Tools.InputResponses, out _));
        Assert.Equal("state-token", document.RootElement.GetProperty(McpSpecKeys.Tools.RequestState).GetString());
    }

    [Collection(nameof(PythonRuntimeCollection))]
    public sealed class InvokeWithPythonTests
    {
        [Fact]
        public async Task InvokeToolAsync_MissingSourcePath_ThrowsThroughHostContext()
        {
            var backend = new PythonMcpToolBackend(new PythonExecutor(ExecutionTestHelpers.CreatePythonInitializer()));
            var tool = new McpRegisteredTool
            {
                Id = "tool-1",
                Descriptor = new Tool { Name = "sample" },
                Binding = McpPrimitiveBinding.Create(ExecutionMode.Python, string.Empty, "mod", "run"),
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => backend.InvokeToolAsync(
                    tool,
                    new CallToolRequestParams { Name = "sample" },
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.Current.CancellationToken));
        }

        [Fact]
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

            var executor = new PythonExecutor(initializer);
            var backend = new PythonMcpToolBackend(executor);
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
                    TestContext.Current.CancellationToken);

                Assert.True(result.IsSuccess);
                Assert.NotNull(result.Value);
            }
            finally
            {
                TryDeleteDirectory(toolDir);
            }
        }

        [Fact]
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

            var executor = new PythonExecutor(initializer);
            var backend = new PythonMcpToolBackend(executor);
            var resource = new McpRegisteredResource
            {
                Id = "res-1",
                Descriptor = new Resource { Uri = "test://item", Name = "item" },
                Binding = McpPrimitiveBinding.Create(ExecutionMode.Python, toolPath, "resource_mcp", "item_resource"),
            };

            try
            {
                var result = backend.ReadResource(resource, "test://item", TestContext.Current.CancellationToken);
                var text = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
                Assert.Equal("resource-body", text.Text);
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
