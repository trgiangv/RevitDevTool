using DevTools.AssemblyIsolation.Metadata;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class MetadataAssemblySessionTests
{
    [Fact]
    public void Metadata_session_loads_an_assembly_without_running_its_module_initializer()
    {
        using var directory = new TemporaryDirectory();
        var markerPath = Path.Combine(directory.Path, "metadata-initializer-ran.txt");
        var assemblyPath = CopyFixture("IsolationSibling", "IsolationSibling.dll", directory.Path);
        Environment.SetEnvironmentVariable("DEVTOOLS_ISOLATION_SIBLING_MARKER", markerPath);

        try
        {
            using var session = MetadataAssemblySession.Create(assemblyPath, RuntimeAssemblyPaths());
            var assembly = session.LoadEntryAssembly();

            Assert.Equal("IsolationSibling", assembly.GetName().Name);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEVTOOLS_ISOLATION_SIBLING_MARKER", null);
        }
    }

    [Fact]
    public void Metadata_session_rejects_duplicate_full_identities_deterministically()
    {
        using var directory = new TemporaryDirectory();
        var first = CopyFixture("IsolationSibling", "first.dll", directory.Path);
        var second = CopyFixture("IsolationSibling", "second.dll", directory.Path);

        var error = Assert.Throws<InvalidOperationException>(
            () => MetadataAssemblySession.Create(first, [second, ..RuntimeAssemblyPaths()]));

        Assert.Contains("Duplicate metadata assembly identity", error.Message, StringComparison.Ordinal);
        Assert.Contains(first, error.Message, StringComparison.Ordinal);
        Assert.Contains(second, error.Message, StringComparison.Ordinal);
    }

    static IEnumerable<string> RuntimeAssemblyPaths() =>
        Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll");

    internal static string CopyFixture(string projectName, string destinationName, string directory)
    {
        var assemblyName = projectName switch
        {
            "PrivateSystemNamedDependency" => "System.Private.IsolationFixture",
            "PrivateAfterDisposeDependency" => "System.Private.AfterDisposeFixture",
            "SameSimpleNameV1" or "SameSimpleNameV2" => "SameSimpleName",
            _ => projectName
        };
        var source = Path.Combine(FindRepositoryRoot(), "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", projectName,
            "bin", "Debug", "net10.0-windows", assemblyName + ".dll");
        var destination = Path.Combine(directory, destinationName);
        File.Copy(source, destination);
        return destination;
    }

    internal static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DevTools.AssemblyIsolation.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
