using System.Reflection;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Loading;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class AssemblyLoaderTests
{
    [Fact]
    public void Load_path_preserves_the_physical_location()
    {
        using var directory = new LoadTestDirectory();
        var path = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "entry.dll", directory.Path);
        var loader = new AssemblyLoader();

        var assembly = loader.LoadPath(path);

        Assert.Equal(Path.GetFullPath(path), assembly.Location, ignoreCase: true);
    }

    [Fact]
    public void Load_path_returns_the_initial_instance_for_the_same_full_identity_from_another_path()
    {
        using var directory = new LoadTestDirectory();
        var firstPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "first.dll", directory.Path);
        var secondPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "second.dll", directory.Path);
        var loader = new AssemblyLoader();

        var first = loader.LoadPath(firstPath);
        var second = loader.LoadPath(secondPath);

        Assert.Same(first, second);
    }

    [Fact]
    public void Load_path_does_not_report_a_stable_same_identity_alias_as_changed()
    {
        using var directory = new LoadTestDirectory();
        var firstPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "first.dll", directory.Path);
        var secondPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "second.dll", directory.Path);
        File.AppendAllBytes(secondPath, [0]);
        var diagnostics = new RecordingDiagnosticSink();
        var loader = new AssemblyLoader(diagnostics);
        _ = loader.LoadPath(firstPath);
        _ = loader.LoadPath(secondPath);
        diagnostics.Diagnostics.Clear();

        _ = loader.LoadPath(secondPath);

        Assert.DoesNotContain(diagnostics.Diagnostics, diagnostic => diagnostic.Code == "path-changed");
    }

    [Fact]
    public void Registered_loader_probes_its_directory_for_managed_dependencies()
    {
        using var directory = new LoadTestDirectory();
        var entryPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "IsolationEntry.dll", directory.Path);
        MetadataAssemblySessionTests.CopyFixture("PrivateSystemNamedDependency", "System.Private.IsolationFixture.dll", directory.Path);
        using var loader = new AssemblyLoader();
        loader.Register(directory.Path);
        loader.Register(directory.Path);
        var entry = loader.LoadPath(entryPath);

        var result = (string)entry.GetType("IsolationEntry.Entry")!
            .GetMethod("GetPrivateDependencyName")!
            .Invoke(null, null)!;

        Assert.Equal("System.Private.IsolationFixture", new AssemblyName(result).Name);
    }

    [Fact]
    public void Disposed_loader_no_longer_probes_its_directory_and_disposal_is_idempotent()
    {
        using var directory = new LoadTestDirectory();
        var entryPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "IsolationEntry.dll", directory.Path);
        MetadataAssemblySessionTests.CopyFixture("PrivateAfterDisposeDependency", "System.Private.AfterDisposeFixture.dll", directory.Path);
        var loader = new AssemblyLoader();
        loader.Register(directory.Path);
        var entry = loader.LoadPath(entryPath);
        loader.Dispose();
        loader.Dispose();

        var exception = Assert.Throws<TargetInvocationException>(() => entry.GetType("IsolationEntry.Entry")!
            .GetMethod("GetAfterDisposeDependencyName")!
            .Invoke(null, null));

        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void Registered_loader_probes_its_directory_for_unmanaged_dependencies()
    {
        using var directory = new LoadTestDirectory();
        var source = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        var destination = Path.Combine(directory.Path, "resolver-native-fixture.dll");
        File.Copy(source, destination);
        using var loader = new AssemblyLoader();
        loader.Register(directory.Path);

        var resolvedPath = loader.FindUnmanagedPathForTesting("resolver-native-fixture");

        Assert.Equal(destination, resolvedPath, ignoreCase: true);
    }

    sealed class RecordingDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public List<AssemblyIsolationDiagnostic> Diagnostics { get; } = [];

        public void Publish(AssemblyIsolationDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }
}

internal sealed class LoadTestDirectory : IDisposable
{
    public LoadTestDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DevTools.AssemblyIsolation.LoadTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Path-loaded assemblies remain locked for the process lifetime.
        }
        catch (UnauthorizedAccessException)
        {
            // Path-loaded assemblies remain locked for the process lifetime.
        }
    }
}
