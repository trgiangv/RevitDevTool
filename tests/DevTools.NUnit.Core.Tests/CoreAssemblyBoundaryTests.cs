using DevTools.NUnit.Core.Runtime;

namespace DevTools.NUnit.Core.Tests;

public sealed class CoreAssemblyBoundaryTests
{
    [Fact]
    public void Contract_assembly_does_not_reference_transport_implementations()
    {
        var references = typeof(INUnitRuntimeSession).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("DevTools.Ipc", references);
        Assert.DoesNotContain("System.Text.Json", references);
    }
}
