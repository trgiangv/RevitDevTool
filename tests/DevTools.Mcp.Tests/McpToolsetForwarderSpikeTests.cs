using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Mcp.Adapter;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Tests.Harness;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

/// <summary>
/// Validates ILRepack MCP-exclude spike on <c>samples/McpToolsetDemo</c>
/// built as <c>Release.Autodesk.2025</c> (external MCP refs + ALC host resolve).
/// </summary>
public sealed class McpToolsetForwarderSpikeTests
{
    private static readonly string ToolsetDllPath = ResolveToolsetDllPath();

    [Fact]
    public void RepackedToolset_KeepsExternalMcpRefs_AndStripsSiblingDlls()
    {
        Assert.True(File.Exists(ToolsetDllPath), BuildHint);

        var toolsetDir = Path.GetDirectoryName(ToolsetDllPath)!;
        Assert.Empty(Directory.GetFiles(toolsetDir, "ModelContextProtocol*.dll"));

        var toolsetAsm = Assembly.LoadFrom(ToolsetDllPath);
        var mcpRefs = toolsetAsm.GetReferencedAssemblies()
            .Where(static name => name.Name?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true)
            .Select(static name => name.Name)
            .ToList();

        Assert.Contains("ModelContextProtocol.Core", mcpRefs);
        Assert.True(mcpRefs.Count >= 1, "Expected external MCP assembly references after ILRepack exclude.");
    }

    [Fact]
    public void LoadedToolset_NativeCallToolResult_IsHostTypeIdentity_WhenHostMcpMatchesToolsetTfm()
    {
        Assert.True(File.Exists(ToolsetDllPath), BuildHint);

        // Toolset is net8.0-windows (Revit 2025). This test host is net10 + net10 MCP — skip cross-TFM invoke.
        if (!string.Equals(
                typeof(CallToolResult).Assembly.GetName().Version?.ToString(),
                "2.0.0.0",
                StringComparison.Ordinal))
        {
            return;
        }

        var hostTfm = typeof(CallToolResult).Assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName;
        if (!string.Equals(hostTfm, ".NETCoreApp,Version=v8.0", StringComparison.Ordinal))
        {
            return;
        }

        using var context = new McpToolsetContext(ToolsetDllPath);
        var assembly = context.LoadAssembly();

        var spikeType = assembly.GetType("McpToolsetDemo.McpForwarderSpikeTool", throwOnError: true)!;
        var method = spikeType.GetMethod(
            "TestForwarderCallToolResult",
            BindingFlags.Public | BindingFlags.Static)!;

        var request = DotnetToolsetTestHarness.CreateRequest();
        var raw = DotnetToolsetTestHarness.InvokeRaw(method, request);

        Assert.NotNull(raw);
        Assert.Same(typeof(CallToolResult), raw.GetType());

        var mapped = ToolsetResultSerializer.ToInvocationResponse(raw, null);
        Assert.Equal("forwarder-spike-ok", McpToolInvoke.Text(mapped));

        var sdk = SdkInvocationMapper.ToSdk(mapped);
        var wire = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);
        Assert.Contains("\"text\":\"forwarder-spike-ok\"", wire, StringComparison.Ordinal);
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

        return Path.Combine(
            "samples",
            "McpToolsetDemo",
            "bin",
            "Release.Autodesk.2025",
            "McpToolsetDemo.dll");
    }

    private const string BuildHint =
        "Build toolset first: dotnet build samples/McpToolsetDemo -c Release.Autodesk.2025 -m:1";
}
