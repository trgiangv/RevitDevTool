using DevTools.Testing.Abstractions.Loading;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class DiscoveryAssemblyLoadTests
{
    [Fact]
    public void Open_without_refs_reuses_already_loaded_assembly()
    {
        var path = typeof(DiscoveryAssemblyLoadTests).Assembly.Location;

        using var load = DiscoveryAssemblyLoad.Open(path);

        Assert.Same(typeof(DiscoveryAssemblyLoadTests).Assembly, load.Assembly);
    }

    [Fact]
    public void Open_with_refs_loads_from_the_copied_assembly_path()
    {
        var directory = Directory.CreateTempSubdirectory("abstractions-discovery-load-").FullName;
        try
        {
            var assemblyPath = Path.Combine(directory, "Host.Tests.dll");
            File.Copy(typeof(DiscoveryAssemblyLoadTests).Assembly.Location, assemblyPath);
            var dependencyPath = Path.Combine(directory, "Dependency.dll");
            File.Copy(typeof(DiscoveryAssemblyLoadTests).Assembly.Location, dependencyPath);
            File.WriteAllText(
                Path.Combine(directory, "Host.Tests.discovery-refs.txt"),
                dependencyPath);

            using (var load = DiscoveryAssemblyLoad.Open(assemblyPath))
            {
                Assert.Equal(Path.GetFullPath(assemblyPath), Path.GetFullPath(load.Assembly.Location));
            }
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [Fact]
    public void Dispose_can_be_called_multiple_times()
    {
        var load = DiscoveryAssemblyLoad.Open(typeof(DiscoveryAssemblyLoadTests).Assembly.Location);
        load.Dispose();
        load.Dispose();
    }
}
