using System.Reflection;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Tests.Harness;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

/// <summary>
/// Toolset MCP exclude + host ILRepack embed: toolsets strip siblings; collectible ALC shares ModelContextProtocol* with default context.
/// Asserts built Autodesk 2025/2027 host outputs: no MCP siblings, ILRepacked host DLL.
/// </summary>
public sealed class McpSharedRuntimePackagingTests
{
    [Fact]
    public void HostBuilds_DoNotShipMcpSiblings()
    {
        var dirs = DiscoverHostOutputDirs().ToList();
        Assert.True(dirs.Count > 0, HostBuildHint);

        foreach (var hostOutputDir in dirs)
            AssertHostEmbeddedMcp(hostOutputDir);
    }

    [Fact]
    public void ToolsetBuilds_StripMcpSiblings_KeepExternalRef()
    {
        var dlls = DiscoverToolsetDlls().ToList();
        Assert.True(dlls.Count > 0, ToolsetBuildHint);

        foreach (var toolsetDllPath in dlls)
        {
            var toolsetDir = Path.GetDirectoryName(toolsetDllPath)!;
            Assert.Empty(Directory.GetFiles(toolsetDir, "ModelContextProtocol*.dll"));

            using var context = new McpToolsetContext(toolsetDllPath);
            var toolsetAsm = context.LoadAssembly();
            Assert.Contains(
                toolsetAsm.GetReferencedAssemblies().Select(static a => a.Name),
                name => string.Equals(name, "ModelContextProtocol.Core", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void DotnetParser_ParsesCompileOnlyMcpToolset_WithoutMcpSiblings()
    {
        var toolsetDllPath = DiscoverToolsetDlls().FirstOrDefault();
        Assert.True(toolsetDllPath is not null, ToolsetBuildHint);

        var parser = new McpAssemblyParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<McpAssemblyParser>.Instance);
        var catalog = parser.ParseCatalogFromAssembly(toolsetDllPath);

        Assert.Contains(catalog.Tools, t => t.Descriptor.Name == "test_forwarder_calltoolresult");
        Assert.Contains(catalog.Tools, t => t.Descriptor.Name == "get_demo_status");
    }

    [Fact]
    public void LoadedToolset_ResolvesCallToolResult_FromHostMcp()
    {
        var pairs = DiscoverHostOutputDirs()
            .Select(dir => (HostDir: dir, Toolset: MatchingToolsetDll(dir)))
            .Where(pair => pair.Toolset is not null)
            .ToList();
        Assert.True(pairs.Count > 0, $"{HostBuildHint} {ToolsetBuildHint}");

        var matched = false;
        foreach (var (_, toolsetDllPath) in pairs)
        {
            using var context = new McpToolsetContext(toolsetDllPath!);
            var assembly = context.LoadAssembly();
            var requestedProtocol = assembly.GetReferencedAssemblies()
                .SingleOrDefault(static a => string.Equals(a.Name, "ModelContextProtocol.Core", StringComparison.Ordinal));
            if (requestedProtocol?.Version != typeof(CallToolResult).Assembly.GetName().Version)
                continue;

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
                continue;
            }

            Assert.NotNull(raw);
            Assert.Same(typeof(CallToolResult), raw.GetType());
            Assert.False(ToolsetResultSerializer.IsForeignCallToolResultType(raw.GetType()));
            matched = true;
        }

        Assert.True(matched, "No host/toolset year pair shared CallToolResult identity. Build the same Autodesk year for both.");
    }

    private static void AssertHostEmbeddedMcp(string hostOutputDir)
    {
        var hostDll = Path.Combine(hostOutputDir, "RevitDevTool.dll");
        Assert.True(File.Exists(hostDll), HostBuildHint);
        Assert.Empty(Directory.GetFiles(hostOutputDir, "ModelContextProtocol*.dll"));
        Assert.True(
            new FileInfo(hostDll).Length > 5_000_000,
            $"Expected ILRepacked host with embedded MCP in {hostOutputDir}.");
    }

    private static IEnumerable<string> DiscoverHostOutputDirs()
    {
        var bin = Path.Combine(FindRepositoryRoot(), "source", "RevitDevTool", "bin");
        if (!Directory.Exists(bin))
            yield break;

        foreach (var configDir in Directory.GetDirectories(bin, "*Autodesk.*"))
        {
            var name = Path.GetFileName(configDir);
            if (!name.Contains("2025", StringComparison.Ordinal) &&
                !name.Contains("2027", StringComparison.Ordinal))
                continue;

            if (File.Exists(Path.Combine(configDir, "RevitDevTool.dll")))
                yield return configDir;
        }
    }

    private static IEnumerable<string> DiscoverToolsetDlls()
    {
        var bin = Path.Combine(FindRepositoryRoot(), "samples", "McpToolsetDemo", "bin");
        if (!Directory.Exists(bin))
            yield break;

        foreach (var configDir in Directory.GetDirectories(bin, "*Autodesk.*"))
        {
            var name = Path.GetFileName(configDir);
            if (!name.Contains("2025", StringComparison.Ordinal) &&
                !name.Contains("2027", StringComparison.Ordinal))
                continue;

            var dll = Path.Combine(configDir, "McpToolsetDemo.dll");
            if (File.Exists(dll))
                yield return dll;
        }
    }

    private static string? MatchingToolsetDll(string hostOutputDir)
    {
        var yearSuffix = Path.GetFileName(hostOutputDir);
        var dll = Path.Combine(FindRepositoryRoot(), "samples", "McpToolsetDemo", "bin", yearSuffix, "McpToolsetDemo.dll");
        return File.Exists(dll) ? dll : null;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RevitDevTool.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private const string HostBuildHint =
        "Build host: dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025";

    private const string ToolsetBuildHint =
        "Build toolset: dotnet build samples/McpToolsetDemo -c Debug.Autodesk.2025";
}
