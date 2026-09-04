using System.Reflection;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests.Isolation;

/// <summary>
/// Documents ADR 0019 identity gap: host ILRepack removes <c>ModelContextProtocol.Core</c> assembly
/// identity; kernel <c>Pin</c> is simple-name keyed only — automatic bind from repacked host is not implemented.
/// </summary>
public sealed class McpMergedHostIdentityTests
{
    private const string Adr0019Gap =
        "ADR 0019 §7: Pin keys shares by simple name only; repacked host removes ModelContextProtocol.Core identity. "
        + "Automatic toolset ALC bind from host load context for merged MCP is not implemented in the isolation kernel.";

    [Fact]
    public void PinShareTable_KeysByCallToolResultAssemblySimpleName()
    {
        var contractAssembly = typeof(CallToolResult).Assembly;
        var contractName = contractAssembly.GetName().Name
            ?? throw new InvalidOperationException("CallToolResult assembly must have a simple name.");

        var plan = McpToolsetIsolationPlan.Create(ResolvePlanEntryPath());
        Assert.True(plan.TryShare(contractAssembly.GetName(), out var pinned));
        Assert.Same(contractAssembly, pinned);

        var requestedCore = new AssemblyName("ModelContextProtocol.Core")
        {
            Version = contractAssembly.GetName().Version,
        };

        if (!string.Equals(contractName, "ModelContextProtocol.Core", StringComparison.Ordinal))
        {
            Assert.False(
                plan.TryShare(requestedCore, out _),
                $"{Adr0019Gap} TryShare('ModelContextProtocol.Core') must not hit when the host contract lives in '{contractName}'.");
        }
        else
        {
            Assert.True(
                plan.TryShare(requestedCore, out var sharedCore),
                "xunit test host ships ModelContextProtocol.Core as a sibling DLL; Pin masks the ADR 0019 merged-host gap.");
            Assert.Same(contractAssembly, sharedCore);
        }
    }

    [Fact]
    [Trait("Category", "HostIdentity")]
    public void LoadedToolset_ResolvesCallToolResult_FromHostMcp()
    {
        // xunit ships ModelContextProtocol.Core beside the test host — Pin shares test-host MCP, not
        // ILRepacked RevitDevTool.dll, so identity assertions here are false-green (architecture review S5-D).
        if (HasSeparateMcpContractAssembly())
        {
            Assert.Skip(
                $"{Adr0019Gap} This xunit process loads CallToolResult from ModelContextProtocol.Core.dll; "
                + "toolset ALC Pin would share test-host MCP, not a repacked host. "
                + "Use live host checklist: docs/agents/mcp-integration-test.md (repacked-host toolset invoke).");
        }

        var pairs = DiscoverHostOutputDirs()
            .Select(dir => (HostDir: dir, Toolset: MatchingToolsetDll(dir)))
            .Where(pair => pair.Toolset is not null)
            .ToList();
        if (pairs.Count == 0)
            Assert.Skip($"{HostBuildHint} {ToolsetBuildHint}");

        var matched = false;
        foreach (var (hostDir, toolsetDllPath) in pairs)
        {
            AssertRepackedHostLayout(hostDir);

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
            matched = true;
        }

        if (!matched)
            Assert.Skip("No host/toolset year pair shared CallToolResult identity. Build the same Autodesk year for both.");
    }

    [Fact]
    [Trait("Category", "HostIdentity")]
    public void RepackedHost_WhenBuilt_ToolsetLoadDocumentsIdentityGap()
    {
        var hostDir = ResolveDefaultRepackedHostDir();
        if (hostDir is null)
        {
            Assert.Skip($"{HostBuildHint} Optional fixture: ILRepacked RevitDevTool.dll not present.");
        }

        AssertRepackedHostLayout(hostDir);

        var toolsetDll = MatchingToolsetDll(hostDir);
        if (toolsetDll is null)
        {
            Assert.Skip($"{ToolsetBuildHint} Matching McpToolsetDemo build not found for {Path.GetFileName(hostDir)}.");
        }

        if (HasSeparateMcpContractAssembly())
        {
            Assert.Skip(
                $"{Adr0019Gap} xunit false-green: ModelContextProtocol.Core is a separate assembly in this process. "
                + "Repacked host layout verified; live invoke belongs in docs/agents/mcp-integration-test.md.");
        }

        using var context = new McpToolsetContext(toolsetDll);
        var assembly = context.LoadAssembly();
        var spikeType = assembly.GetType("McpToolsetDemo.McpForwarderSpikeTool", throwOnError: true)!;
        var method = spikeType.GetMethod("TestForwarderCallToolResult", BindingFlags.Public | BindingFlags.Static)!;
        var raw = DotnetToolsetTestHarness.InvokeRaw(method, DotnetToolsetTestHarness.CreateRequest());

        Assert.NotNull(raw);
        Assert.False(
            raw is CallToolResult,
            $"{Adr0019Gap} Without ModelContextProtocol.Core share hit, toolset must return a non-host CallToolResult identity.");
    }

    private static bool HasSeparateMcpContractAssembly() =>
        string.Equals(
            typeof(CallToolResult).Assembly.GetName().Name,
            "ModelContextProtocol.Core",
            StringComparison.Ordinal);

    private static string ResolvePlanEntryPath()
    {
        var toolset = DiscoverToolsetDlls().FirstOrDefault();
        if (toolset is not null)
            return toolset;

        return typeof(McpMergedHostIdentityTests).Assembly.Location;
    }

    private static string? ResolveDefaultRepackedHostDir()
    {
        var candidate = Path.Combine(
            FindRepositoryRoot(),
            "source",
            "RevitDevTool",
            "bin",
            "Debug.Autodesk.2025");
        return IsRepackedHostLayout(candidate) ? candidate : null;
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

    private static void AssertRepackedHostLayout(string hostOutputDir)
    {
        var hostDll = Path.Combine(hostOutputDir, "RevitDevTool.dll");
        OptionalArtifact.RequireFile(hostDll, HostBuildHint);
        Assert.Empty(Directory.GetFiles(hostOutputDir, "ModelContextProtocol*.dll"));
        Assert.True(
            new FileInfo(hostDll).Length > 5_000_000,
            $"Expected ILRepacked host with embedded MCP in {hostOutputDir}. {Adr0019Gap}");
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
        "Build host: dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false";

    private const string ToolsetBuildHint =
        "Build toolset: dotnet build samples/McpToolsetDemo/McpToolsetDemo.csproj -c Debug.Autodesk.2025 -m:1";
}
