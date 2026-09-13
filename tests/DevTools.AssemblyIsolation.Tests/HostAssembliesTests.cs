using System.Reflection;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class HostAssembliesTests
{
    [TestMethod]
    public void All_captures_type_anchors_once_and_skips_missing_names()
    {
        var host = new StubHostAssemblies();
        var first = host.All();
        var second = host.All();

        Assert.AreSame(first, second);
        Assert.AreSame(typeof(HostAssembliesTests).Assembly, first.Single());
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
