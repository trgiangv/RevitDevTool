using System.Reflection;
using System.Runtime.Loader;
using DevTools.AssemblyIsolation;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DevTools.Execution.Tests.AssemblyIsolation;

public sealed class CommandAssemblyIsolationTests
{
    [Fact]
    public void Command_plan_loads_private_system_named_dependencies_lazily_without_locking_their_files()
    {
        using var workload = CommandFixtureWorkload.Create(includeSibling: true);
        using var session = AssemblyIsolationSession.Create(
            CommandIsolationPlan.Create(workload.EntryPath, Array.Empty<Assembly>()));

        var entry = session.LoadEntryAssembly();
        var dependencyName = (string)entry.GetType("IsolationEntry.Entry")!
            .GetMethod("GetPrivateDependencyName")!
            .Invoke(null, null)!;
        var dependency = AssemblyLoadContext.GetLoadContext(entry)!.Assemblies
            .Single(assembly => string.Equals(assembly.GetName().Name, "System.Private.IsolationFixture", StringComparison.Ordinal));

        Assert.Equal("System.Private.IsolationFixture", new AssemblyName(dependencyName).Name);
        Assert.Same(AssemblyLoadContext.GetLoadContext(entry), AssemblyLoadContext.GetLoadContext(dependency));
        Assert.False(File.Exists(workload.SiblingInitializerMarkerPath));

        using var writable = new FileStream(workload.DependencyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Command_plan_returns_the_explicit_parent_contract_instance()
    {
        var parent = typeof(CommandAssemblyIsolationTests).Assembly;
        using var session = AssemblyIsolationSession.Create(
            CommandIsolationPlan.Create(parent.Location, [parent]));

        Assert.Same(parent, session.LoadEntryAssembly());
    }

    [Fact]
    public void Command_plan_releases_its_collectible_context_after_execution_references_are_cleared()
    {
        using var workload = CommandFixtureWorkload.Create(includeSibling: false);
        using var session = CreateAndLoad(workload.EntryPath);

        var result = session.VerifyUnload();

        Assert.True(result.IsCollectible);
        Assert.True(result.IsUnloaded, result.Detail);
    }

    [Fact]
    public void Command_plan_resolves_a_transitive_sibling_graph()
    {
        using var graph = DynamicCommandGraph.Create("transitive");
        using var session = AssemblyIsolationSession.Create(CommandIsolationPlan.Create(graph.EntryPath, Array.Empty<Assembly>()));

        var value = (string)session.LoadEntryAssembly().GetType("Fixture.Entry")!
            .GetMethod("Value")!.Invoke(null, null)!;

        Assert.Equal("transitive", value);
    }

    [Fact]
    public void Command_plan_keeps_conflicting_private_dependency_versions_in_their_own_sessions()
    {
        using var first = DynamicCommandGraph.Create("one", new Version(1, 0, 0, 0));
        using var second = DynamicCommandGraph.Create("two", new Version(2, 0, 0, 0));
        using var firstSession = AssemblyIsolationSession.Create(CommandIsolationPlan.Create(first.EntryPath, Array.Empty<Assembly>()));
        using var secondSession = AssemblyIsolationSession.Create(CommandIsolationPlan.Create(second.EntryPath, Array.Empty<Assembly>()));

        Assert.Equal("one", InvokeEntry(firstSession));
        Assert.Equal("two", InvokeEntry(secondSession));
    }

    [Fact]
    public void Command_plan_reuses_official_wpf_ui_packages_from_the_default_context()
    {
        using var graph = WpfSharingCommandGraph.Create();
        var plan = CommandIsolationPlan.Create(graph.EntryPath, Array.Empty<Assembly>());

        Assert.True(plan.TryShare(new AssemblyName("MahApps.Metro"), out var bound));
        Assert.Equal("MahApps.Metro", bound.GetName().Name);

        using var session = AssemblyIsolationSession.Create(plan);
        var value = (string)session.LoadEntryAssembly().GetType("Fixture.Entry")!
            .GetMethod("Value")!.Invoke(null, null)!;

        Assert.Equal("official", value);
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(bound));
        Assert.NotSame(AssemblyLoadContext.GetLoadContext(session.LoadEntryAssembly()), AssemblyLoadContext.GetLoadContext(bound));

        var again = CommandIsolationPlan.Create(graph.EntryPath, Array.Empty<Assembly>());
        Assert.True(again.TryShare(new AssemblyName("MahApps.Metro"), out var rebound));
        Assert.Same(bound, rebound);
    }

    [Fact]
    public void Command_plan_does_not_share_devtools_forked_wpf_ui_assemblies()
    {
        using var graph = WpfSharingCommandGraph.CreateFork();
        var plan = CommandIsolationPlan.Create(graph.EntryPath, Array.Empty<Assembly>());

        Assert.False(plan.TryShare(new AssemblyName("DevTools.MahApps.Metro"), out _));
        Assert.Contains(plan.ManagedSources, source => source.Resolve(new AssemblyName("DevTools.MahApps.Metro")) is not null);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static AssemblyIsolationSession CreateAndLoad(string entryPath)
    {
        var session = AssemblyIsolationSession.Create(
            CommandIsolationPlan.Create(entryPath, Array.Empty<Assembly>()));
        _ = session.LoadEntryAssembly();
        return session;
    }

    private static string InvokeEntry(AssemblyIsolationSession session) => (string)session.LoadEntryAssembly()
        .GetType("Fixture.Entry")!.GetMethod("Value")!.Invoke(null, null)!;
}

internal sealed class DynamicCommandGraph : IDisposable
{
    DynamicCommandGraph(string directory) => Directory = directory;

    public string Directory { get; }
    public string EntryPath => Path.Combine(Directory, "Entry.dll");

    public static DynamicCommandGraph Create(string value, Version? dependencyVersion = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Execution.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var graph = new DynamicCommandGraph(directory);
        var leaf = Path.Combine(directory, "Private.Dependency.dll");
        var middle = Path.Combine(directory, "Middle.dll");
        Compile(leaf, "Private.Dependency", $"[assembly:System.Reflection.AssemblyVersion(\"{dependencyVersion ?? new Version(1, 0, 0, 0)}\")] namespace Fixture {{ public static class Leaf {{ public static string Value => \"{value}\"; }} }}");
        Compile(middle, "Middle", "namespace Fixture { public static class Middle { public static string Value => Leaf.Value; } }", [leaf]);
        Compile(graph.EntryPath, "Entry", "namespace Fixture { public static class Entry { public static string Value() => Middle.Value; } }", [middle]);
        return graph;
    }

    internal static void Compile(string path, string assemblyName, string source, IEnumerable<string>? references = null)
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).ToList();
        if (references is not null) trusted.AddRange(references.Select(path => MetadataReference.CreateFromFile(path)));
        var compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(source)], trusted,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = File.Create(path);
        var result = compilation.Emit(stream);
        if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
    }
}

internal sealed class CommandFixtureWorkload : IDisposable
{
    CommandFixtureWorkload(string directory, string markerPath)
    {
        Directory = directory;
        SiblingInitializerMarkerPath = markerPath;
    }

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "IsolationEntry.dll");

    public string DependencyPath => Path.Combine(Directory, "System.Private.IsolationFixture.dll");

    public string SiblingInitializerMarkerPath { get; }

    public static CommandFixtureWorkload Create(bool includeSibling)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Execution.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var markerPath = Path.Combine(directory, "sibling-initializer-ran.txt");

        CopyFixture("IsolationEntry", "IsolationEntry.dll", directory);
        CopyFixture("PrivateSystemNamedDependency", "System.Private.IsolationFixture.dll", directory);
        if (includeSibling)
        {
            Environment.SetEnvironmentVariable("DEVTOOLS_ISOLATION_SIBLING_MARKER", markerPath);
            CopyFixture("IsolationSibling", "IsolationSibling.dll", directory);
        }

        return new CommandFixtureWorkload(directory, markerPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DEVTOOLS_ISOLATION_SIBLING_MARKER", null);
        if (System.IO.Directory.Exists(Directory))
            System.IO.Directory.Delete(Directory, recursive: true);
    }

    static void CopyFixture(string projectName, string assemblyName, string directory)
    {
        var source = Path.Combine(FindRepositoryRoot(), "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", projectName,
            "bin", "Debug", "net10.0-windows", assemblyName);
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

internal sealed class WpfSharingCommandGraph : IDisposable
{
    WpfSharingCommandGraph(string directory) => Directory = directory;

    public string Directory { get; }
    public string EntryPath => Path.Combine(Directory, "Entry.dll");

    public static WpfSharingCommandGraph Create()
    {
        var graph = new WpfSharingCommandGraph(CreateDirectory());
        var mahApps = Path.Combine(graph.Directory, "MahApps.Metro.dll");
        DynamicCommandGraph.Compile(
            mahApps,
            "MahApps.Metro",
            """
            [assembly:System.Reflection.AssemblyVersion("1.0.0.0")]
            namespace Fixture { public static class Marker { public static string Id => "official"; } }
            """);
        DynamicCommandGraph.Compile(
            graph.EntryPath,
            "Entry",
            "namespace Fixture { public static class Entry { public static string Value() => Marker.Id; } }",
            [mahApps]);
        return graph;
    }

    public static WpfSharingCommandGraph CreateFork()
    {
        var graph = new WpfSharingCommandGraph(CreateDirectory());
        var fork = Path.Combine(graph.Directory, "DevTools.MahApps.Metro.dll");
        DynamicCommandGraph.Compile(
            fork,
            "DevTools.MahApps.Metro",
            """
            [assembly:System.Reflection.AssemblyVersion("1.0.0.0")]
            namespace Fixture { public static class Marker { public static string Id => "fork"; } }
            """);
        DynamicCommandGraph.Compile(
            graph.EntryPath,
            "Entry",
            "namespace Fixture { public static class Entry { public static string Value() => Marker.Id; } }",
            [fork]);
        return graph;
    }

    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // Default-context LoadFrom keeps official WPF UI DLLs mapped until process exit.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Execution.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        return directory;
    }
}
