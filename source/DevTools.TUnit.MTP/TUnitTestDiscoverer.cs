using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TUnit.Runtime;

namespace DevTools.TUnit.MTP;

[UsedImplicitly]
public sealed class TUnitTestDiscoverer : ITestDiscoverer
{
    public IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection) =>
        TUnitCatalog.Discover(assemblyPath, selection);
}
