using DevTools.AssemblyIsolation;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class SharedSidecarsTests
{
    [TestMethod]
    [DataRow("MahApps.Metro", true)]
    [DataRow("controlzex", true)]
    [DataRow("Microsoft.Xaml.Behaviors", true)]
    [DataRow("DevTools.UI", false)]
    [DataRow(null, false)]
    public void Contains_recognizes_known_sidecar_simple_names(string? name, bool expected)
    {
        Assert.AreEqual(expected, SharedSidecars.Contains(name));
    }

    [TestMethod]
    public void ShareFromDirectory_requires_directory()
    {
        var plan = AssemblyIsolationPlan.Create("entry.dll");
        Assert.ThrowsExactly<ArgumentException>(() => SharedSidecars.ShareFromDirectory(plan, " "));
    }
}
