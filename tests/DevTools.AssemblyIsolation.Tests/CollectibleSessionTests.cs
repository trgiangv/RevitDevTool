using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class CollectibleSessionTests
{
    [Fact]
    public void Collectible_session_returns_the_explicitly_parent_bound_entry_assembly()
    {
        var entry = typeof(CollectibleSessionTests).Assembly;
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(entry.Location)
                .WithLifecycle(AssemblyIsolationLifecycle.Collectible)
                .BindToParent(entry));

        Assert.Same(entry, session.LoadEntryAssembly());
    }

    [Fact]
    public void Collectible_session_rejects_an_incompatible_parent_before_private_fallback()
    {
        var entry = typeof(CollectibleSessionTests).Assembly;
        var incompatibleParent = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(entry.GetName().Name!) { Version = new Version(99, 0, 0, 0) },
            AssemblyBuilderAccess.Run);
        var plan = AssemblyIsolationPlan.Create(entry.Location)
            .WithLifecycle(AssemblyIsolationLifecycle.Collectible)
            .BindToParent(incompatibleParent)
            .AddManagedSource(new DirectoryAssemblySource(Path.GetDirectoryName(entry.Location)!));

        using var session = AssemblyIsolationSession.Create(plan);

        Assert.Throws<AssemblyIdentityMismatchException>(session.LoadEntryAssembly);
    }

    [Fact]
    public void Collectible_session_keeps_a_workload_local_system_named_dependency_private_and_leaves_its_files_writable()
    {
        using var workload = FixtureWorkload.Create();
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(workload.EntryPath)
                .WithLifecycle(AssemblyIsolationLifecycle.Collectible)
                .AddManagedSource(new DirectoryAssemblySource(workload.Directory)));

        var entry = session.LoadEntryAssembly();
        var dependencyName = (string)entry.GetType("IsolationEntry.Entry")!
            .GetMethod("GetPrivateDependencyName")!
            .Invoke(null, null)!;
        var dependency = AssemblyLoadContext.GetLoadContext(entry)!.Assemblies
            .Single(assembly => string.Equals(assembly.GetName().Name, "System.Private.IsolationFixture", StringComparison.Ordinal));

        Assert.Equal("System.Private.IsolationFixture", new AssemblyName(dependencyName).Name);
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(entry));
        Assert.Same(AssemblyLoadContext.GetLoadContext(entry), AssemblyLoadContext.GetLoadContext(dependency));

        using var writable = new FileStream(workload.DependencyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Collectible_session_does_not_load_unrequested_siblings()
    {
        using var workload = FixtureWorkload.Create(includeSibling: true);
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(workload.EntryPath)
                .WithLifecycle(AssemblyIsolationLifecycle.Collectible)
                .AddManagedSource(new DirectoryAssemblySource(workload.Directory)));

        _ = session.LoadEntryAssembly();

        Assert.False(File.Exists(workload.SiblingInitializerMarkerPath));
    }

    [Fact]
    public void Collectible_session_releases_the_context_after_dispose()
    {
        using var workload = FixtureWorkload.Create();
        using var session = CreateAndLoad(workload.EntryPath, workload.Directory);

        var result = session.VerifyUnload();

        Assert.True(result.IsCollectible);
        Assert.True(result.IsUnloaded, result.Detail);
    }

    static AssemblyIsolationSession CreateAndLoad(string entryPath, string directory)
    {
        var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(entryPath)
                .WithLifecycle(AssemblyIsolationLifecycle.Collectible)
                .AddManagedSource(new DirectoryAssemblySource(directory)));
        _ = session.LoadEntryAssembly();
        return session;
    }
}

sealed class FixtureWorkload : IDisposable
{
    readonly string markerPath;

    FixtureWorkload(string directory, string markerPath)
    {
        Directory = directory;
        this.markerPath = markerPath;
    }

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "IsolationEntry.dll");

    public string DependencyPath => Path.Combine(Directory, "System.Private.IsolationFixture.dll");

    public string SiblingInitializerMarkerPath => markerPath;

    public static FixtureWorkload Create(bool includeSibling = false)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.AssemblyIsolation.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var markerPath = Path.Combine(directory, "sibling-initializer-ran.txt");

        CopyFixture("IsolationEntry", "IsolationEntry.dll", directory);
        CopyFixture("PrivateSystemNamedDependency", "System.Private.IsolationFixture.dll", directory);
        if (includeSibling)
        {
            Environment.SetEnvironmentVariable("DEVTOOLS_ISOLATION_SIBLING_MARKER", markerPath);
            CopyFixture("IsolationSibling", "IsolationSibling.dll", directory);
        }

        return new FixtureWorkload(directory, markerPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DEVTOOLS_ISOLATION_SIBLING_MARKER", null);
        if (System.IO.Directory.Exists(Directory))
            System.IO.Directory.Delete(Directory, recursive: true);
    }

    static void CopyFixture(string projectName, string assemblyName, string directory)
    {
        var source = Path.Combine(FindRepositoryRoot(), "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", projectName, "bin", "Debug", "net10.0-windows", assemblyName);
        File.Copy(source, Path.Combine(directory, assemblyName));
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
