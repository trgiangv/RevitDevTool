using DevTools.AssemblyIsolation;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class SharedSidecarsTests
{
    [Theory]
    [InlineData("MahApps.Metro", true)]
    [InlineData("controlzex", true)]
    [InlineData("Microsoft.Xaml.Behaviors", true)]
    [InlineData("DevTools.UI", false)]
    [InlineData(null, false)]
    public void Contains_recognizes_known_sidecar_simple_names(string? name, bool expected)
    {
        Assert.Equal(expected, SharedSidecars.Contains(name));
    }

    [Fact]
    public void ShareFromDirectory_requires_directory()
    {
        var plan = AssemblyIsolationPlan.Create("entry.dll");
        Assert.Throws<ArgumentException>(() => SharedSidecars.ShareFromDirectory(plan, " "));
    }
}
