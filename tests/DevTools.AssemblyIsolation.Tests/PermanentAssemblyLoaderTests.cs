using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Loading;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class PermanentAssemblyLoaderTests
{
    [Fact]
    public void Load_path_preserves_the_physical_location_for_permanent_dependencies()
    {
        using var directory = new PermanentLoadDirectory();
        var path = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "entry.dll", directory.Path);
        var loader = new PermanentAssemblyLoader();

        var assembly = loader.LoadPath(path);

        Assert.Equal(Path.GetFullPath(path), assembly.Location, ignoreCase: true);
    }

    [Fact]
    public void Load_path_returns_the_initial_instance_for_the_same_full_identity_from_another_path()
    {
        using var directory = new PermanentLoadDirectory();
        var firstPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "first.dll", directory.Path);
        var secondPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "second.dll", directory.Path);
        var loader = new PermanentAssemblyLoader();

        var first = loader.LoadPath(firstPath);
        var second = loader.LoadPath(secondPath);

        Assert.Same(first, second);
    }

    [Fact]
    public void Load_path_does_not_report_a_stable_same_identity_alias_as_changed()
    {
        using var directory = new PermanentLoadDirectory();
        var firstPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "first.dll", directory.Path);
        var secondPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "second.dll", directory.Path);
        File.AppendAllBytes(secondPath, [0]);
        var diagnostics = new RecordingDiagnosticSink();
        var loader = new PermanentAssemblyLoader(diagnostics);
        _ = loader.LoadPath(firstPath);
        _ = loader.LoadPath(secondPath);
        diagnostics.Diagnostics.Clear();

        _ = loader.LoadPath(secondPath);

        Assert.DoesNotContain(diagnostics.Diagnostics, diagnostic => diagnostic.Code == "permanent-path-changed");
    }

    sealed class RecordingDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public List<AssemblyIsolationDiagnostic> Diagnostics { get; } = [];

        public void Publish(AssemblyIsolationDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }
}

internal sealed class PermanentLoadDirectory : IDisposable
{
    public PermanentLoadDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DevTools.AssemblyIsolation.PermanentLoadTests",
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
            // Permanent path-loaded assemblies remain locked for the process lifetime.
        }
        catch (UnauthorizedAccessException)
        {
            // Permanent path-loaded assemblies remain locked for the process lifetime.
        }
    }
}
