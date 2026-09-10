namespace DevTools.TUnit.Runtime.Tests;

public sealed class TUnitExpansionTests
{
    [Fact]
    public void Class_arguments_display_name_is_used_when_the_method_has_none()
    {
        Assert.Equal("neg", TUnitExpansion.CombinationDisplayName(null, "neg"));
        Assert.Equal("unit", TUnitExpansion.CombinationDisplayName(null, "unit"));
    }

    [Fact]
    public void Method_arguments_display_name_wins_over_class()
    {
        Assert.Equal("Unit_X", TUnitExpansion.CombinationDisplayName("Unit_X", "unit"));
    }
}
