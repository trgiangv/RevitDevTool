using DevTools.Execution.External.Mcp.Registry;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using DevTools.Mcp.Catalog.Discovery;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Execution.Tests;

[Collection(nameof(PythonRuntimeCollection))]
public sealed class PythonMcpRegistryProviderTests
{
    public PythonMcpRegistryProviderTests()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        _ = ExecutionTestHelpers.EnsurePixiPythonInitializedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void LoadCatalog_WhenPythonNotReady_ReturnsEmpty()
    {
        var provider = new PythonMcpRegistryProvider(
            ExecutionTestHelpers.CreatePythonInitializer(),
            new PythonExecutor(ExecutionTestHelpers.CreatePythonInitializer()),
            new PythonToolsetParser(NullLogger<PythonToolsetParser>.Instance),
            NullLogger<PythonMcpRegistryProvider>.Instance);

        provider.ConfigurePaths([ExecutionTestHelpers.CreateTempDirectory("mcp-empty")]);
        var catalog = provider.LoadCatalog();

        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);
    }

    [Fact]
    public async Task LoadCatalog_WithMissingDirectory_LogsAndReturnsEmpty()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = CreateProvider(initializer);
        var missing = Path.Combine(Path.GetTempPath(), $"missing-toolset-{Guid.NewGuid():N}");

        provider.ConfigurePaths([missing]);
        var catalog = provider.LoadCatalog();

        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);
    }

    [Fact]
    public async Task LoadCatalog_WithRegistryScript_ReturnsParsedCatalog()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var toolsetDir = ExecutionTestHelpers.CreateTempDirectory("mcp-toolset");
        var toolPath = Path.Combine(toolsetDir, "echo_tool_mcp.py");
        File.WriteAllText(toolPath, """
            from mcp.server.mcpserver import MCPServer

            mcp = MCPServer("registry-toolset")

            @mcp.tool()
            def echo_tool(message: str = "hello") -> str:
                return message
            """);
        File.WriteAllText(Path.Combine(toolsetDir, "__mcp_registry__.py"), "# anchor");

        try
        {
            var provider = CreateProvider(initializer);
            provider.ConfigurePaths([toolsetDir]);
            var catalog = provider.LoadCatalog();

            Assert.Single(catalog.Tools);
            Assert.Equal("echo_tool", catalog.Tools[0].Descriptor.Name);
        }
        finally
        {
            TryDeleteDirectory(toolsetDir);
        }
    }

    [Fact]
    public async Task LoadCatalog_WithMcpEntryFile_PreResolvesDependencies()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var toolsetDir = ExecutionTestHelpers.CreateTempDirectory("mcp-entry");
        var toolPath = Path.Combine(toolsetDir, "sample_mcp.py");
        File.WriteAllText(toolPath, """
            # /// script
            # dependencies = []
            # ///
            def register():
                return {"tools": [], "resources": []}
            """);

        try
        {
            var provider = CreateProvider(initializer);
            provider.ConfigurePaths([toolsetDir]);
            var catalog = provider.LoadCatalog();
            Assert.Empty(catalog.Tools);
        }
        finally
        {
            TryDeleteDirectory(toolsetDir);
        }
    }

    private static PythonMcpRegistryProvider CreateProvider(PythonInitializer initializer) =>
        new(
            initializer,
            new PythonExecutor(initializer),
            new PythonToolsetParser(NullLogger<PythonToolsetParser>.Instance),
            NullLogger<PythonMcpRegistryProvider>.Instance);

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
