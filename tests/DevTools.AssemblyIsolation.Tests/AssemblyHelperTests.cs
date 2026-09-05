using System.Reflection;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class AssemblyHelperTests
{
    [Fact]
    public void Find_returns_an_assembly_already_in_the_default_context()
    {
        var loaded = typeof(AssemblyHelperTests).Assembly;
        var simpleName = loaded.GetName().Name!;

        Assert.Same(loaded, AssemblyHelper.Find(simpleName));
        Assert.Same(loaded, AssemblyHelper.Find(simpleName.ToUpperInvariant()));
    }

    [Fact]
    public void Find_does_not_load_a_missing_simple_name()
    {
        Assert.Null(AssemblyHelper.Find("DevTools.Missing.HostApi"));
        Assert.Null(AssemblyHelper.Find(" "));
    }

    [Fact]
    public void Find_many_skips_missing_names_and_collapses_duplicates()
    {
        var loaded = typeof(AssemblyHelperTests).Assembly;
        var simpleName = loaded.GetName().Name!;

        var found = AssemblyHelper.FindMany(
        [
            "DevTools.Missing.HostApi",
            simpleName,
            simpleName.ToLowerInvariant(),
        ]).ToArray();

        Assert.Same(loaded, Assert.Single(found));
    }

    [Fact]
    public void Find_many_rejects_a_null_name_list()
    {
        Assert.Throws<ArgumentNullException>(() => AssemblyHelper.FindMany(null!).ToArray());
    }
}
