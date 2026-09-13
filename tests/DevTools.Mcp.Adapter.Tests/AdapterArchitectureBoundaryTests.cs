using System.Reflection;

namespace DevTools.Mcp.Adapter.Tests;

[TestClass]
public sealed class AdapterArchitectureBoundaryTests
{
    [TestMethod]
    public void Execution_DoesNotReferenceHostMcpAdapter()
    {
        var executionReferences = Assembly.Load("DevTools.Execution").GetReferencedAssemblies();

        Assert.IsFalse(executionReferences.Any(reference =>
            string.Equals(reference.Name, "DevTools.Mcp.Adapter", StringComparison.Ordinal)));
    }
}
