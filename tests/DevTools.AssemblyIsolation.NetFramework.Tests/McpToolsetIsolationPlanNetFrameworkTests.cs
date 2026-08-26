using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.Mcp.Catalog.Discovery;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

public sealed class McpToolsetIsolationPlanNetFrameworkTests
{
    [Fact]
    public void Scoped_mcp_context_loads_a_private_sibling_dependency_on_net_framework()
    {
        using var workload = McpFixtureWorkload.Create();

        var plan = McpToolsetIsolationPlan.Create(workload.EntryPath);
        using var context = new McpToolsetContext(workload.EntryPath);
        var entry = context.LoadAssembly();
        var method = entry.GetType("IsolationEntry.Entry", throwOnError: true)!
            .GetMethod("GetPrivateDependencyName", BindingFlags.Public | BindingFlags.Static)!;

        var dependencyName = (string)method.Invoke(null, null)!;

        Assert.Equal(AssemblyIsolationKind.Isolated, plan.Kind);
        Assert.Single(plan.ManagedSources);
        Assert.Empty(plan.NativeSources);
        Assert.Equal("System.Private.IsolationFixture", new AssemblyName(dependencyName).Name);
    }
}

sealed class McpFixtureWorkload : IDisposable
{
    McpFixtureWorkload(string directory) => Directory = directory;

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "IsolationEntry.dll");

    public static McpFixtureWorkload Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Mcp.NetFramework.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        CopyFixture("IsolationEntry", "IsolationEntry.dll", directory);
        CopyFixture("PrivateSystemNamedDependency", "System.Private.IsolationFixture.dll", directory);
        return new McpFixtureWorkload(directory);
    }

    public void Dispose()
    {
        FixtureWorkload.TryDeleteLoadedDirectory(Directory);
    }

    static void CopyFixture(string projectName, string assemblyName, string destination)
    {
        var source = Path.Combine(FindRepositoryRoot(), "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", projectName,
            "bin", "Debug", "net48", assemblyName);
        File.Copy(source, Path.Combine(destination, assemblyName));
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
