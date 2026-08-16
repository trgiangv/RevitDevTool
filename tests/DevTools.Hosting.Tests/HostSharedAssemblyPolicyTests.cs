using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.Utilities.AssemblyLoading;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Hosting.Tests;

[CollectionDefinition("HostSharedAssemblies", DisableParallelization = true)]
public sealed class HostSharedAssembliesCollection;

[Collection("HostSharedAssemblies")]
public sealed class HostSharedAssemblyPolicyTests
{
    [Fact]
    public void Use_revit_policy_shares_RevitAPI()
    {
        HostSharedAssemblies.Use(new RevitSharedAssemblyPolicy());
        Assert.True(HostSharedAssemblies.IsShared("RevitAPI"));
        Assert.True(HostSharedAssemblies.IsExplicitHostAssembly("RevitAPI"));
        Assert.True(HostSharedAssemblies.IsShared("Autodesk.Revit.DB"));
        Assert.True(HostSharedAssemblies.IsShared("MahApps.Metro"));
    }

    [Fact]
    public void Use_acad_policy_shares_acmgd()
    {
        HostSharedAssemblies.Use(new AcadSharedAssemblyPolicy());
        Assert.True(HostSharedAssemblies.IsShared("acmgd"));
        Assert.True(HostSharedAssemblies.IsExplicitHostAssembly("acmgd"));
        Assert.True(HostSharedAssemblies.IsShared("Autodesk.AutoCAD.DatabaseServices"));
    }

    [Fact]
    public void AddRevitInProcess_registers_policy_and_does_not_register_launch()
    {
        var services = new ServiceCollection();
        services.AddRevitInProcess();
        using var provider = services.BuildServiceProvider();

        var policy = provider.GetRequiredService<IHostSharedAssemblyPolicy>();
        Assert.IsType<RevitSharedAssemblyPolicy>(policy);
        Assert.Same(policy, provider.GetRequiredService<IHostSharedAssemblyPolicy>());
        Assert.True(HostSharedAssemblies.IsShared("RevitAPI"));
        Assert.Empty(provider.GetServices<IHostLaunchService>());
    }

    [Fact]
    public void AddAutocadInProcess_registers_policy_and_does_not_register_launch()
    {
        var services = new ServiceCollection();
        services.AddAutocadInProcess();
        using var provider = services.BuildServiceProvider();

        var policy = provider.GetRequiredService<IHostSharedAssemblyPolicy>();
        Assert.IsType<AcadSharedAssemblyPolicy>(policy);
        Assert.True(HostSharedAssemblies.IsShared("acmgd"));
        Assert.Empty(provider.GetServices<IHostLaunchService>());
    }

    [Fact]
    public void Execution_Abstractions_owns_ui_package_prefixes()
    {
        var path = Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.Execution.Abstractions",
            "HostPackagePrefixes.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("MahApps.", text, StringComparison.Ordinal);
        Assert.Contains("ControlzEx.", text, StringComparison.Ordinal);
        Assert.Contains("CommunityToolkit.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_ins_call_in_process_and_not_launch()
    {
        var root = RepositoryRoot.Find();
        AssertAddIn(
            Path.Combine(root, "source", "RevitDevTool"),
            "AddRevitInProcess");
        AssertAddIn(
            Path.Combine(root, "source", "AcadDevTool"),
            "AddAutocadInProcess");
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
                Assert.DoesNotContain("AddRevitInProcess", text, StringComparison.Ordinal);
                Assert.DoesNotContain("AddAutocadInProcess", text, StringComparison.Ordinal);
            }
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

    private static void AssertAddIn(string projectDir, string inProcessMethod)
    {
        var sources = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);
        var combined = string.Join('\n', sources.Select(File.ReadAllText));
        Assert.Contains(inProcessMethod, combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddRevitLaunch", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAutocadFamilyLaunch", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostLaunchCore", combined, StringComparison.Ordinal);
    }
}
