using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;

namespace DevTools.Mcp.Catalog.Tests;

/// <summary>
/// Layout-only packaging checks: toolsets strip MCP siblings; host ILRepack embeds MCP (no siblings, large DLL).
/// CallToolResult identity on repacked host is covered by <see cref="Isolation.McpMergedHostIdentityTests"/>
/// (skips in xunit when ModelContextProtocol.Core is a separate assembly — false-green guard).
/// </summary>
public sealed class McpSharedRuntimePackagingTests
{
    [Fact]
    public void HostBuilds_DoNotShipMcpSiblings()
    {
        var dirs = DiscoverHostOutputDirs().ToList();
        if (dirs.Count == 0)
            Assert.Skip(HostBuildHint);

        foreach (var hostOutputDir in dirs)
            AssertHostEmbeddedMcp(hostOutputDir);
    }

    [Fact]
    public void ToolsetBuilds_StripMcpSiblings_KeepExternalRef()
    {
        var dlls = DiscoverToolsetDlls().ToList();
        if (dlls.Count == 0)
            Assert.Skip(ToolsetBuildHint);

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
        if (toolsetDllPath is null)
            Assert.Skip(ToolsetBuildHint);

        var parser = new McpAssemblyParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<McpAssemblyParser>.Instance);
        var catalog = parser.ParseCatalogFromAssembly(toolsetDllPath);

        Assert.Contains(catalog.Tools, t => t.Descriptor.Name == "test_forwarder_calltoolresult");
        Assert.Contains(catalog.Tools, t => t.Descriptor.Name == "get_demo_status");
    }

    private static bool IsRepackedHostLayout(string hostOutputDir)
    {
        var hostDll = Path.Combine(hostOutputDir, "RevitDevTool.dll");
        if (!File.Exists(hostDll))
            return false;
        if (Directory.GetFiles(hostOutputDir, "ModelContextProtocol*.dll").Length > 0)
            return false;
        return new FileInfo(hostDll).Length > 5_000_000;
    }

    private static void AssertHostEmbeddedMcp(string hostOutputDir)
    {
        var hostDll = Path.Combine(hostOutputDir, "RevitDevTool.dll");
        OptionalArtifact.RequireFile(hostDll, HostBuildHint);
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

            if (IsRepackedHostLayout(configDir))
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
        "ILRepack host (no ModelContextProtocol*.dll siblings): build with ILRepackable=true, e.g. scripts/build-host.ps1 -Year 2025";

    private const string ToolsetBuildHint =
        "Build toolset: dotnet build samples/McpToolsetDemo -c Debug.Autodesk.2025";
}
