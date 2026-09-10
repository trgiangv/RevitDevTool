namespace DevTools.TUnit.Runtime.Tests;

public sealed class TUnitMetadataNamesTests
{
    [Fact]
    public void Formats_double_and_task_like_tunit_engine()
    {
        Assert.Equal("System.Double", TUnitMetadataNames.Of(typeof(double)));
        Assert.Equal("System.Threading.Tasks.Task", TUnitMetadataNames.Of(typeof(Task)));
    }

    [Fact]
    public void Formats_constructed_generics_as_rfc_0017()
    {
        Assert.Equal(
            "System.Collections.Generic.List`1<System.String>",
            TUnitMetadataNames.Of(typeof(List<string>)));
    }

    [Fact]
    public void Formats_arrays_pointers_byref_and_generic_parameters()
    {
        Assert.Equal("System.Int32[]", TUnitMetadataNames.Of(typeof(int[])));
        Assert.Equal("System.Int32*", TUnitMetadataNames.Of(typeof(int).MakePointerType()));
        Assert.Equal("System.Int32&", TUnitMetadataNames.Of(typeof(int).MakeByRefType()));
        Assert.Equal("!0", TUnitMetadataNames.Of(typeof(List<>).GetGenericArguments()[0]));
        Assert.Equal(
            "!!0",
            TUnitMetadataNames.Of(typeof(Array)
                .GetMethod(nameof(Array.Empty))!
                .GetGenericArguments()[0]));
    }
}
