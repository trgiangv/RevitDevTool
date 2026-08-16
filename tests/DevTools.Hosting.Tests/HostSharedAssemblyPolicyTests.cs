using DevTools.Utilities.AssemblyLoading;

namespace DevTools.Hosting.Tests;

[CollectionDefinition("HostSharedAssemblies", DisableParallelization = true)]
public sealed class HostSharedAssembliesCollection;

[Collection("HostSharedAssemblies")]
public sealed class HostSharedAssemblyPolicyTests
{
    [Fact]
    public void Add_ins_call_use_at_startup_and_not_launch()
    {
        var root = RepositoryRoot.Find();
        AssertAddIn(Path.Combine(root, "source", "RevitDevTool"));
        AssertAddIn(Path.Combine(root, "source", "AcadDevTool"));
    }

    [Fact]
    public void Out_of_process_hosts_do_not_register_in_process_policy()
    {
        var root = RepositoryRoot.Find();
        foreach (var project in new[] { "DevTools.Daemon", "DevTools.NUnit.Runner", "DevTools.Mcp.Server" })
        {
            foreach (var path in Directory.GetFiles(
                         Path.Combine(root, "source", project), "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);
                Assert.DoesNotContain("HostSharedAssemblies.Use", text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Generic_Hosting_has_no_shared_assembly_policy()
    {
        var hostingDir = Path.Combine(RepositoryRoot.Find(), "source", "DevTools.Hosting");
        foreach (var path in Directory.GetFiles(hostingDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("HostApiAssemblySet", text, StringComparison.Ordinal);
            Assert.DoesNotContain("HostSharedAssemblies", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NUnit_Host_does_not_reference_Execution()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.NUnit.Host",
            "DevTools.NUnit.Host.csproj"));
        Assert.Contains("DevTools.Execution.Abstractions", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain(@"..\DevTools.Execution\DevTools.Execution.csproj", csproj, StringComparison.Ordinal);
    }

    private static void AssertAddIn(string projectDir)
    {
        var sources = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);
        var combined = string.Join('\n', sources.Select(File.ReadAllText));
        Assert.Contains("HostSharedAssemblies.Use", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddRevitLaunch", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAutocadFamilyLaunch", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostLaunchCore", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddRevitInProcess", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAutocadInProcess", combined, StringComparison.Ordinal);
    }
}
