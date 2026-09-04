using System.Reflection;

namespace DevTools.Mcp.Adapter.Tests;

public sealed class AdapterArchitectureBoundaryTests
{
    [Fact]
    public void Execution_DoesNotReferenceHostMcpAdapter()
    {
        var executionReferences = Assembly.Load("DevTools.Execution").GetReferencedAssemblies();

        Assert.DoesNotContain(executionReferences, reference =>
            string.Equals(reference.Name, "DevTools.Mcp.Adapter", StringComparison.Ordinal));
    }
}
