using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

[TestClass]
public sealed class CommandIsolationPlanNetFrameworkTests
{
    [TestMethod]
    public void Scoped_command_plan_loads_a_private_sibling_dependency_on_net_framework()
    {
        using var workload = CommandFixtureWorkload.Create();

        var plan = CommandIsolationPlan.Create(workload.EntryPath, Array.Empty<Assembly>());
        using var session = AssemblyIsolationSession.Create(plan);
        var entry = session.LoadEntryAssembly();
        var method = entry.GetType("IsolationEntry.Entry", throwOnError: true)!
            .GetMethod("GetPrivateDependencyName", BindingFlags.Public | BindingFlags.Static)!;

        var dependencyName = (string)method.Invoke(null, null)!;

        Assert.AreEqual(AssemblyIsolationKind.Isolated, plan.Kind);
        Assert.IsTrue(plan.LoadsFromDistinctFile);
        Assert.AreEqual(1, plan.ManagedSources.Count);
        Assert.IsEmpty(plan.NativeSources);
        Assert.AreEqual("System.Private.IsolationFixture", new AssemblyName(dependencyName).Name);
        Assert.IsFalse(string.Equals(
            Path.GetFullPath(workload.EntryPath),
            plan.EntryAssemblyPath,
            StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(plan.EntryAssemblyPath.StartsWith(
            Path.GetTempPath(),
            StringComparison.OrdinalIgnoreCase));
        using (new FileStream(workload.EntryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
    }

    [TestMethod]
    public void Shared_entry_skips_the_temp_copy()
    {
        var entry = typeof(CommandIsolationPlanNetFrameworkTests).Assembly;
        var plan = CommandIsolationPlan.Create(entry.Location, [entry]);

        Assert.AreEqual(Path.GetFullPath(entry.Location), plan.EntryAssemblyPath);
        Assert.IsTrue(plan.LoadsFromDistinctFile);
        using var session = AssemblyIsolationSession.Create(plan);
        Assert.AreSame(entry, session.LoadEntryAssembly());
    }

    [TestMethod]
    public void Rebuilt_command_overrides_the_previous_load_of_the_same_identity()
    {
        using var workload = OverrideWorkload.Create("old");
        var firstPlan = CommandIsolationPlan.Create(workload.EntryPath, Array.Empty<Assembly>());
        using var firstSession = AssemblyIsolationSession.Create(firstPlan);
        var first = firstSession.LoadEntryAssembly();

        Assert.AreEqual("old", InvokeMarker(first));

        OverrideWorkload.Write(workload.EntryPath, "new");
        var secondPlan = CommandIsolationPlan.Create(workload.EntryPath, Array.Empty<Assembly>());
        using var secondSession = AssemblyIsolationSession.Create(secondPlan);
        var second = secondSession.LoadEntryAssembly();

        Assert.AreEqual("new", InvokeMarker(second));
        Assert.AreEqual("old", InvokeMarker(first));
        Assert.AreNotSame(first, second);
        Assert.IsFalse(string.Equals(
            firstPlan.EntryAssemblyPath,
            secondPlan.EntryAssemblyPath,
            StringComparison.OrdinalIgnoreCase));
    }

    static string InvokeMarker(Assembly assembly) => (string)assembly.GetType("Fixture.Entry", throwOnError: true)!
        .GetMethod("Value", BindingFlags.Public | BindingFlags.Static)!
        .Invoke(null, null)!;
}

sealed class OverrideWorkload : IDisposable
{
    OverrideWorkload(string directory) => Directory = directory;

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "OverrideFixture.dll");

    public static OverrideWorkload Create(string marker)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Command.NetFramework.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var workload = new OverrideWorkload(directory);
        Write(workload.EntryPath, marker);
        return workload;
    }

    public static void Write(string path, string marker)
    {
        var directory = Path.GetDirectoryName(path)!;
        System.IO.Directory.CreateDirectory(directory);
        var compilation = CSharpCompilation.Create(
            "OverrideFixture",
            [CSharpSyntaxTree.ParseText(
                "[assembly:System.Reflection.AssemblyVersion(\"1.0.0.0\")] "
                + "namespace Fixture { public static class Entry { public static string Value() => \""
                + marker
                + "\"; } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = File.Create(path);
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
    }

    public void Dispose()
    {
        FixtureWorkload.TryDeleteLoadedDirectory(Directory);
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
