namespace DevTools.TUnit.Runtime.Tests;

[TestClass]
public sealed class TUnitExpansionTests
{
    [TestMethod]
    public void Class_arguments_display_name_is_used_when_the_method_has_none()
    {
        Assert.AreEqual("neg", TUnitExpansion.CombinationDisplayName(null, "neg"));
        Assert.AreEqual("unit", TUnitExpansion.CombinationDisplayName(null, "unit"));
    }

    [TestMethod]
    public void Method_arguments_display_name_wins_over_class()
    {
        Assert.AreEqual("Unit_X", TUnitExpansion.CombinationDisplayName("Unit_X", "unit"));
    }
}
