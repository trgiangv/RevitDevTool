using System.Reflection;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class AssemblyHelperTests
{
    [TestMethod]
    public void Find_returns_an_assembly_already_in_the_default_context()
    {
        var loaded = typeof(AssemblyHelperTests).Assembly;
        var simpleName = loaded.GetName().Name!;

        Assert.AreSame(loaded, AssemblyHelper.Find(simpleName));
        Assert.AreSame(loaded, AssemblyHelper.Find(simpleName.ToUpperInvariant()));
    }

    [TestMethod]
    public void Find_does_not_load_a_missing_simple_name()
    {
        Assert.IsNull(AssemblyHelper.Find("DevTools.Missing.HostApi"));
        Assert.IsNull(AssemblyHelper.Find(" "));
    }

    [TestMethod]
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

        Assert.AreSame(loaded, found.Single());
    }

    [TestMethod]
    public void Find_many_rejects_a_null_name_list()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => AssemblyHelper.FindMany(null!).ToArray());
    }
}
