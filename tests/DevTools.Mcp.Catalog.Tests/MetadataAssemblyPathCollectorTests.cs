using System.Reflection;
using DevTools.Mcp.Catalog.Isolation;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class MetadataAssemblyPathCollectorTests
{
    private static readonly Type CollectorType =
        typeof(McpToolsetContext).Assembly.GetType("DevTools.Mcp.Catalog.Isolation.MetadataAssemblyPathCollector", throwOnError: true)!;

    [Fact]
    public void Collect_IncludesEntryDirectoryAndExplicitDependencies()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Mcp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var entry = Path.Combine(directory, "Toolset.dll");
        File.WriteAllText(entry, string.Empty);
        var extra = Path.Combine(directory, "Extra.dll");
        File.WriteAllText(extra, string.Empty);

        try
        {
            var paths = InvokeCollect(entry, [extra]);

            Assert.Contains(Path.GetFullPath(entry), paths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(Path.GetFullPath(extra), paths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Collect_Throws_WhenExplicitDependencyPathMissing()
    {
        var entry = typeof(MetadataAssemblyPathCollectorTests).Assembly.Location;

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeCollect(entry, [@"C:\missing\dependency.dll"]));

        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void GetMetadataTypes_ReturnsTypesFromAssembly()
    {
        var method = CollectorType.GetMethod("GetMetadataTypes", BindingFlags.Public | BindingFlags.Static)!;
        var types = (IReadOnlyList<Type>)method.Invoke(null, [typeof(MetadataAssemblyPathCollectorTests).Assembly])!;

        Assert.Contains(typeof(MetadataAssemblyPathCollectorTests), types);
    }

    private static IReadOnlyList<string> InvokeCollect(string entryPath, IEnumerable<string>? dependencyPaths)
    {
        var method = CollectorType.GetMethod("Collect", BindingFlags.Public | BindingFlags.Static)!;
        return (IReadOnlyList<string>)method.Invoke(null, [entryPath, dependencyPaths])!;
    }
}
