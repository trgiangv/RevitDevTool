using DevTools.AssemblyIsolation.Loading;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

public sealed class PermanentAssemblyLoaderNetFrameworkTests
{
    [Fact]
    public void Load_path_preserves_the_physical_location_for_permanent_dependencies()
    {
        using var workload = PermanentFixtureWorkload.Create();
        var loader = new PermanentAssemblyLoader();

        var assembly = loader.LoadPath(workload.EntryPath);

        Assert.Equal(Path.GetFullPath(workload.EntryPath), assembly.Location, ignoreCase: true);
    }
}

sealed class PermanentFixtureWorkload : IDisposable
{
    PermanentFixtureWorkload(string directory) => Directory = directory;

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "IsolationEntry.dll");

    public static PermanentFixtureWorkload Create()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "DevTools.AssemblyIsolation.NetFramework.PermanentLoadTests",
            Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var source = Path.Combine(
            FindRepositoryRoot(),
            "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", "IsolationEntry",
            "bin", "Debug", "net48", "IsolationEntry.dll");
        File.Copy(source, Path.Combine(directory, "IsolationEntry.dll"));
        return new PermanentFixtureWorkload(directory);
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
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
