using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonSourceInvokeTests
{
    [TestMethod]
    public void WriteRequest_NullRequest_ReturnsEmptyObject()
    {
        Assert.AreEqual("{}", PythonSource.WriteRequest(null));
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

        var json = PythonSource.WriteRequest(request);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual("hello", document.RootElement.GetProperty("arguments").GetProperty("message").GetString());
    }

    [TestMethod]
    public void WriteRequest_IgnoresInputResponsesAndRequestState()
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

        var json = PythonSource.WriteRequest(request);
        using var document = JsonDocument.Parse(json);

        Assert.IsTrue(document.RootElement.TryGetProperty(McpSpecKeys.Tools.Arguments, out _));
        Assert.IsFalse(document.RootElement.TryGetProperty(McpSpecKeys.Tools.InputResponses, out _));
        Assert.IsFalse(document.RootElement.TryGetProperty(McpSpecKeys.Tools.RequestState, out _));
    }

    [TestClass]
    public sealed class InvokeWithPythonTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task InvokeToolAsync_MissingSourcePath_ThrowsThroughHostContext()
        {
            var backend = new PythonSource(ExecutionTestHelpers.CreatePythonInitializer());
            var tool = new RegisteredTool
            {
                Id = "tool-1",
                Descriptor = new Tool { Name = "sample" },
                Binding = PrimitiveBinding.Create(ExecutionMode.Python, string.Empty, "mod", "run", "", ""),
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

            var backend = new PythonSource(initializer);
            var tool = new RegisteredTool
            {
                Id = "tool-echo",
                Descriptor = new Tool { Name = "echo" },
                Binding = PrimitiveBinding.Create(ExecutionMode.Python, toolPath, "echo_mcp", "echo", "", ""),
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

                Assert.AreNotEqual(true, result.IsError);
            }
            finally
            {
                TryDeleteDirectory(toolDir);
            }
        }

        [TestMethod]
        public async Task InvokeToolAsync_SecondCallWithSameMtime_DoesNotReloadModule()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var toolDir = ExecutionTestHelpers.CreateTempDirectory("python-mcp-cache");
            var toolPath = Path.Combine(toolDir, "load_count_mcp.py");
            File.WriteAllText(toolPath, """
                _LOAD_COUNT = 0

                from mcp.server.mcpserver import MCPServer

                _LOAD_COUNT += 1
                mcp = MCPServer("load-count-toolset")

                @mcp.tool()
                def count() -> int:
                    return _LOAD_COUNT
                """);

            var backend = new PythonSource(initializer);
            var tool = new RegisteredTool
            {
                Id = "tool-count",
                Descriptor = new Tool { Name = "count" },
                Binding = PrimitiveBinding.Create(ExecutionMode.Python, toolPath, "load_count_mcp", "count", "", ""),
            };
            var request = new CallToolRequestParams { Name = "count" };

            try
            {
                var first = await backend.InvokeToolAsync(
                    tool,
                    request,
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken);
                var second = await backend.InvokeToolAsync(
                    tool,
                    request,
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken);

                Assert.AreNotEqual(true, first.IsError);
                Assert.AreNotEqual(true, second.IsError);
                Assert.AreEqual("1", Assert.IsInstanceOfType<TextContentBlock>(first.Content[0]).Text);
                Assert.AreEqual("1", Assert.IsInstanceOfType<TextContentBlock>(second.Content[0]).Text);
            }
            finally
            {
                TryDeleteDirectory(toolDir);
            }
        }

        [TestMethod]
        public async Task InvokeToolAsync_ClearCaches_ReloadsModuleOnNextCall()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var toolDir = ExecutionTestHelpers.CreateTempDirectory("python-mcp-clear");
            var toolPath = Path.Combine(toolDir, "clear_count_mcp.py");
            File.WriteAllText(toolPath, """
                import uuid

                from mcp.server.mcpserver import MCPServer

                _IMPORT_ID = uuid.uuid4().hex
                mcp = MCPServer("clear-count-toolset")

                @mcp.tool()
                def import_id() -> str:
                    return _IMPORT_ID
                """);

            var backend = new PythonSource(initializer);
            var tool = new RegisteredTool
            {
                Id = "tool-clear-count",
                Descriptor = new Tool { Name = "import_id" },
                Binding = PrimitiveBinding.Create(ExecutionMode.Python, toolPath, "clear_count_mcp", "import_id", "", ""),
            };
            var request = new CallToolRequestParams { Name = "import_id" };

            try
            {
                var first = await backend.InvokeToolAsync(
                    tool,
                    request,
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken);
                var firstId = Assert.IsInstanceOfType<TextContentBlock>(first.Content[0]).Text;
                backend.ClearCaches();
                var second = await backend.InvokeToolAsync(
                    tool,
                    request,
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken);
                var secondId = Assert.IsInstanceOfType<TextContentBlock>(second.Content[0]).Text;

                Assert.AreNotEqual(true, first.IsError);
                Assert.AreNotEqual(true, second.IsError);
                Assert.IsFalse(string.IsNullOrEmpty(firstId));
                Assert.IsFalse(string.IsNullOrEmpty(secondId));
                Assert.AreNotEqual(firstId, secondId);
            }
            finally
            {
                TryDeleteDirectory(toolDir);
            }
        }

        [TestMethod]
        public async Task InvokeToolAsync_NewerMtime_ReloadsModule()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var toolDir = ExecutionTestHelpers.CreateTempDirectory("python-mcp-mtime");
            var toolPath = Path.Combine(toolDir, "mtime_count_mcp.py");
            File.WriteAllText(toolPath, """
                import uuid

                from mcp.server.mcpserver import MCPServer

                _IMPORT_ID = uuid.uuid4().hex
                mcp = MCPServer("mtime-count-toolset")

                @mcp.tool()
                def import_id() -> str:
                    return _IMPORT_ID
                """);

            var backend = new PythonSource(initializer);
            var tool = new RegisteredTool
            {
                Id = "tool-mtime-count",
                Descriptor = new Tool { Name = "import_id" },
                Binding = PrimitiveBinding.Create(ExecutionMode.Python, toolPath, "mtime_count_mcp", "import_id", "", ""),
            };
            var request = new CallToolRequestParams { Name = "import_id" };

            try
            {
                var first = await backend.InvokeToolAsync(
                    tool,
                    request,
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken);
                var firstId = Assert.IsInstanceOfType<TextContentBlock>(first.Content[0]).Text;

                await Task.Delay(20, TestContext.CancellationToken);
                File.SetLastWriteTimeUtc(toolPath, DateTime.UtcNow.AddSeconds(1));
                File.WriteAllText(toolPath, """
                    import uuid

                    from mcp.server.mcpserver import MCPServer

                    _IMPORT_ID = uuid.uuid4().hex
                    mcp = MCPServer("mtime-count-toolset-v2")

                    @mcp.tool()
                    def import_id() -> str:
                        return _IMPORT_ID
                    """);

                var second = await backend.InvokeToolAsync(
                    tool,
                    request,
                    ExecutionTestHelpers.InlineHostContext(),
                    TestContext.CancellationToken);
                var secondId = Assert.IsInstanceOfType<TextContentBlock>(second.Content[0]).Text;

                Assert.AreNotEqual(true, first.IsError);
                Assert.AreNotEqual(true, second.IsError);
                Assert.IsFalse(string.IsNullOrEmpty(firstId));
                Assert.IsFalse(string.IsNullOrEmpty(secondId));
                Assert.AreNotEqual(firstId, secondId);
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

            var backend = new PythonSource(initializer);
            var resource = new RegisteredResource
            {
                Id = "res-1",
                Descriptor = new Resource { Uri = "test://item", Name = "item" },
                Binding = PrimitiveBinding.Create(ExecutionMode.Python, toolPath, "resource_mcp", "item_resource", "", ""),
            };

            try
            {
                var result = await backend.ReadResourceAsync(resource, "test://item", TestContext.CancellationToken);
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
