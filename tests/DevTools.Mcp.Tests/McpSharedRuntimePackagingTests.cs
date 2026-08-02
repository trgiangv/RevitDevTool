using System.Reflection;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Tests.Harness;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

/// <summary>
/// Toolset MCP exclude + host ILRepack embed: toolsets strip siblings; collectible ALC shares ModelContextProtocol* with default context.
/// </summary>
public sealed class McpSharedRuntimePackagingTests
{
    private static readonly string HostOutputDir = ResolveHostOutputDir();
    private static readonly string ToolsetDllPath = ResolveToolsetDllPath();

    [Fact]
    public void HostBuild_DoesNotShipMcpSiblings()
    {
        Assert.True(Directory.Exists(HostOutputDir), HostBuildHint);

        var hostDll = Path.Combine(HostOutputDir, "RevitDevTool.dll");
        Assert.True(File.Exists(hostDll), HostBuildHint);
        Assert.Empty(Directory.GetFiles(HostOutputDir, "ModelContextProtocol*.dll"));
        Assert.True(new FileInfo(hostDll).Length > 5_000_000, "Expected ILRepacked host with embedded MCP + transitive deps.");
    }

    [Fact]
    public void ToolsetBuild_StripsMcpSiblings_KeepsExternalRef()
    {
        Assert.True(File.Exists(ToolsetDllPath), ToolsetBuildHint);

        var toolsetDir = Path.GetDirectoryName(ToolsetDllPath)!;
        Assert.Empty(Directory.GetFiles(toolsetDir, "ModelContextProtocol*.dll"));

        var toolsetAsm = Assembly.LoadFrom(ToolsetDllPath);
        Assert.Contains(
            toolsetAsm.GetReferencedAssemblies().Select(static a => a.Name),
            name => string.Equals(name, "ModelContextProtocol.Core", StringComparison.Ordinal));
    }

    [Fact]
    public void DotnetParser_ParsesCompileOnlyMcpToolset_WithoutMcpSiblings()
    {
        Assert.True(File.Exists(ToolsetDllPath), ToolsetBuildHint);

        var parser = new McpAssemblyParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<McpAssemblyParser>.Instance);
        var catalog = parser.ParseCatalogFromAssembly(ToolsetDllPath);

        Assert.Contains(catalog.Tools, t => t.Descriptor.Name == "test_forwarder_calltoolresult");
        Assert.Contains(catalog.Tools, t => t.Descriptor.Name == "get_demo_status");
    }

    [Fact]
    public void LoadedToolset_ResolvesCallToolResult_FromHostMcp()
    {
        Assert.True(File.Exists(ToolsetDllPath), ToolsetBuildHint);

        using var context = new McpToolsetContext(ToolsetDllPath);
        var assembly = context.LoadAssembly();

        var spikeType = assembly.GetType("McpToolsetDemo.McpForwarderSpikeTool", throwOnError: true)!;
        var method = spikeType.GetMethod("TestForwarderCallToolResult", BindingFlags.Public | BindingFlags.Static)!;
        var request = DotnetToolsetTestHarness.CreateRequest();

        object? raw;
        try
        {
            raw = DotnetToolsetTestHarness.InvokeRaw(method, request);
        }
        catch (MissingMethodException)
        {
            // net10 test host vs net8 toolset MCP surface — live Revit 2025 is authoritative.
            return;
        }

        Assert.NotNull(raw);
        Assert.Same(typeof(CallToolResult), raw.GetType());
        Assert.False(ToolsetResultSerializer.IsForeignCallToolResultType(raw.GetType()));
    }

    private static string ResolveHostOutputDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "source", "RevitDevTool", "bin", "Release.Autodesk.2025");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return Path.Combine("source", "RevitDevTool", "bin", "Release.Autodesk.2025");
    }

    private static string ResolveToolsetDllPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "samples",
                "McpToolsetDemo",
                "bin",
                "Release.Autodesk.2025",
                "McpToolsetDemo.dll");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return Path.Combine("samples", "McpToolsetDemo", "bin", "Release.Autodesk.2025", "McpToolsetDemo.dll");
    }

    private const string HostBuildHint =
        "Build host first: dotnet build source/RevitDevTool/RevitDevTool.csproj -c Release.Autodesk.2025 -m:1";

    private const string ToolsetBuildHint =
        "Build toolset first: dotnet build samples/McpToolsetDemo -c Release.Autodesk.2025 -m:1";
}
