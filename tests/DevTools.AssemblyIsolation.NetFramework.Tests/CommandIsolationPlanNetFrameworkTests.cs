using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.Execution.Providers.Dotnet;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

public sealed class CommandIsolationPlanNetFrameworkTests
{
    [Fact]
    public void Scoped_command_plan_loads_a_private_sibling_dependency_on_net_framework()
    {
        using var workload = CommandFixtureWorkload.Create();

        var plan = CommandIsolationPlan.Create(workload.EntryPath, Array.Empty<Assembly>());
        using var session = AssemblyIsolationSession.Create(plan);
        var entry = session.LoadEntryAssembly();
        var method = entry.GetType("IsolationEntry.Entry", throwOnError: true)!
            .GetMethod("GetPrivateDependencyName", BindingFlags.Public | BindingFlags.Static)!;

        var dependencyName = (string)method.Invoke(null, null)!;

        Assert.Equal(AssemblyIsolationKind.Isolated, plan.Kind);
        Assert.False(plan.LoadsFromDistinctFile);
        Assert.Single(plan.ManagedSources);
        Assert.Empty(plan.NativeSources);
        Assert.Equal("System.Private.IsolationFixture", new AssemblyName(dependencyName).Name);
    }

    [Fact]
    public void Scoped_command_plan_byte_load_does_not_lock_the_project_output()
    {
        using var workload = CommandFixtureWorkload.Create();

        var plan = CommandIsolationPlan.Create(workload.EntryPath, Array.Empty<Assembly>());
        using var session = AssemblyIsolationSession.Create(plan);
        _ = session.LoadEntryAssembly();

        Assert.False(plan.LoadsFromDistinctFile);
        Assert.Equal(Path.GetFullPath(workload.EntryPath), plan.EntryAssemblyPath);
        using (new FileStream(workload.EntryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
    }
}

sealed class CommandFixtureWorkload : IDisposable
{
    CommandFixtureWorkload(string directory) => Directory = directory;

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "IsolationEntry.dll");

    public static CommandFixtureWorkload Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Command.NetFramework.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        CopyFixture("IsolationEntry", "IsolationEntry.dll", directory);
        CopyFixture("PrivateSystemNamedDependency", "System.Private.IsolationFixture.dll", directory);
        return new CommandFixtureWorkload(directory);
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
