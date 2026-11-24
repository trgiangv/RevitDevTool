using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Tests.Documents;

[TestClass]
public sealed class SubcategoryRuleTests
{
    [TestMethod]
    [DataRow("OST_DuctCurves", "OST_DuctCurvesCenterLine")]
    [DataRow("OST_DuctFitting", "OST_DuctFittingCenterLine")]
    [DataRow("OST_FlexDuctCurves", "OST_FlexDuctCurvesCenterLine")]
    [DataRow("OST_PipeCurves", "OST_PipeCurvesCenterLine")]
    [DataRow("OST_Conduit", "OST_ConduitCenterLine")]
    [DataRow("OST_CableTray", "OST_CableTrayCenterLine")]
    [DataRow("OST_FabricationDuctwork", "OST_FabricationDuctworkCenterLine")]
    public void CenterLineIsLeftOut(string parent, string child)
    {
        Assert.IsFalse(SubcategoryRule.Lists(parent, child));
    }

    [TestMethod]
    [DataRow("OST_Site")]
    [DataRow("OST_Lines")]
    [DataRow("OST_Topography")]
    public void NoisyParentListsNoChildren(string parent)
    {
        Assert.IsFalse(SubcategoryRule.ListsChildrenOf(parent));
        Assert.IsFalse(SubcategoryRule.Lists(parent, "OST_SitePoint"));
    }

    [TestMethod]
    [DataRow("OST_Railings", "OST_RailingSupport")]
    [DataRow("OST_Toposolid", "OST_ToposolidSplitLines")]
    [DataRow("OST_Floors", "OST_EdgeSlab")]
    public void OtherSubcategoryIsListed(string parent, string child)
    {
        Assert.IsTrue(SubcategoryRule.Lists(parent, child));
    }
}
