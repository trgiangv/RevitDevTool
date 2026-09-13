namespace DevTools.TUnit.Runtime.Tests;

[TestClass]
public sealed class TUnitMetadataNamesTests
{
    [TestMethod]
    public void Formats_double_and_task_like_tunit_engine()
    {
        Assert.AreEqual("System.Double", TUnitMetadataNames.Of(typeof(double)));
        Assert.AreEqual("System.Threading.Tasks.Task", TUnitMetadataNames.Of(typeof(Task)));
    }

    [TestMethod]
    public void Formats_constructed_generics_as_rfc_0017()
    {
        Assert.AreEqual(
            "System.Collections.Generic.List`1<System.String>",
            TUnitMetadataNames.Of(typeof(List<string>)));
    }

    [TestMethod]
    public void Formats_arrays_pointers_byref_and_generic_parameters()
    {
        Assert.AreEqual("System.Int32[]", TUnitMetadataNames.Of(typeof(int[])));
        Assert.AreEqual("System.Int32*", TUnitMetadataNames.Of(typeof(int).MakePointerType()));
        Assert.AreEqual("System.Int32&", TUnitMetadataNames.Of(typeof(int).MakeByRefType()));
        Assert.AreEqual("!0", TUnitMetadataNames.Of(typeof(List<>).GetGenericArguments()[0]));
        Assert.AreEqual(
            "!!0",
            TUnitMetadataNames.Of(typeof(Array)
                .GetMethod(nameof(Array.Empty))!
                .GetGenericArguments()[0]));
    }
}
