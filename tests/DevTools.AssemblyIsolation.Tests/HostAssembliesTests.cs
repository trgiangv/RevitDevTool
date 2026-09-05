using System.Reflection;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class HostAssembliesTests
{
    [Fact]
    public void All_captures_type_anchors_once_and_skips_missing_names()
    {
        var host = new StubHostAssemblies();
        var first = host.All();
        var second = host.All();

        Assert.Same(first, second);
        Assert.Same(typeof(HostAssembliesTests).Assembly, Assert.Single(first));
    }

    sealed class StubHostAssemblies : HostAssemblies
    {
        protected override IEnumerable<Assembly> LoadedByType
        {
            get { yield return typeof(HostAssembliesTests).Assembly; }
        }

        protected override IReadOnlyList<string> LoadedByName { get; } = ["DevTools.Missing.HostApi"];
    }
}
