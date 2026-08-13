using DevTools.NUnit.Core;
using DevTools.NUnit.TestAdapter.Runner;

namespace DevTools.NUnit.TestAdapter;

/// <summary>
/// Discovers NUnit tests from PE metadata without loading the assembly.
/// Keeps Test Explorer populated when no host pipe is available (pytest-style local collect).
/// </summary>
internal static class LocalNUnitTestDiscoverer
{
    public static IReadOnlyList<RemoteTestCase> Discover(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        return NUnitMetadataDiscoverer.Discover(assemblyPath)
            .Select(test => new RemoteTestCase(test.Id, test.Name, test.FullName, assemblyPath))
            .ToList();
    }
}
