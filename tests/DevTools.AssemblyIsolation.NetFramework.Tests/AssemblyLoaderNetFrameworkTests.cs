using DevTools.AssemblyIsolation.Loading;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

public sealed class AssemblyLoaderNetFrameworkTests
{
    [Fact]
    public void Load_path_preserves_the_physical_location()
    {
        using var workload = LoaderFixtureWorkload.Create();
        var loader = new AssemblyLoader();

        var assembly = loader.LoadPath(workload.EntryPath);

        Assert.Equal(Path.GetFullPath(workload.EntryPath), assembly.Location, ignoreCase: true);
    }
}

sealed class LoaderFixtureWorkload : IDisposable
{
    LoaderFixtureWorkload(string directory) => Directory = directory;

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "IsolationEntry.dll");

    public static LoaderFixtureWorkload Create()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "DevTools.AssemblyIsolation.NetFramework.LoadTests",
            Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var source = Path.Combine(
            FindRepositoryRoot(),
            "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", "IsolationEntry",
            "bin", "Debug", "net48", "IsolationEntry.dll");
        File.Copy(source, Path.Combine(directory, "IsolationEntry.dll"));
        return new LoaderFixtureWorkload(directory);
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
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
