using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Tests.Documents;

[TestClass]
public sealed class CategoryRuleTests
{
    [TestMethod]
    [DataRow(CategoryKind.Model, "OST_Walls")]
    [DataRow(CategoryKind.Model, "OST_DetailComponents")]
    [DataRow(CategoryKind.Annotation, "OST_DuctTags")]
    [DataRow(CategoryKind.Annotation, "OST_Grids")]
    [DataRow(CategoryKind.Annotation, "OST_TitleBlocks")]
    public void VisibleModelOrAnnotationIsListed(CategoryKind kind, string builtIn)
    {
        Assert.IsTrue(CategoryRule.Lists(kind, true, builtIn));
    }

    [TestMethod]
    [DataRow(CategoryKind.Model, "OST_ZoneEquipment")]
    [DataRow(CategoryKind.Annotation, "OST_Constraints")]
    public void HiddenFromUiIsLeftOut(CategoryKind kind, string builtIn)
    {
        Assert.IsFalse(CategoryRule.Lists(kind, false, builtIn));
    }

    [TestMethod]
    [DataRow("OST_Views")]
    [DataRow("OST_Schedules")]
    [DataRow("OST_IOSSketchGrid")]
    [DataRow("OST_SharedBasePoint")]
    public void InternalIsLeftOut(string builtIn)
    {
        Assert.IsFalse(CategoryRule.Lists(CategoryKind.Other, true, builtIn));
    }

    [TestMethod]
    [DataRow(CategoryKind.Annotation, "OST_WeakDims")]
    [DataRow(CategoryKind.Annotation, "OST_SectionBox")]
    [DataRow(CategoryKind.Model, "OST_RvtLinks")]
    [DataRow(CategoryKind.Model, "OST_Materials")]
    public void ExcludedCategoryIsLeftOut(CategoryKind kind, string builtIn)
    {
        Assert.IsFalse(CategoryRule.Lists(kind, true, builtIn));
    }

    [TestMethod]
    [DataRow("OST_Sheets")]
    [DataRow("OST_ProjectInformation")]
    public void SheetsAndProjectInfoLeftOut(string builtIn)
    {
        Assert.IsFalse(CategoryRule.Lists(CategoryKind.Model, true, builtIn));
        Assert.IsFalse(CategoryRule.Lists(CategoryKind.Annotation, true, builtIn));
        Assert.IsFalse(CategoryRule.Lists(CategoryKind.Model, true, builtIn, true));
    }

    [TestMethod]
    public void DetailGroupsAreListed()
    {
        Assert.IsTrue(CategoryRule.Lists(CategoryKind.Other, true, "OST_IOSDetailGroups"));
    }

    [TestMethod]
    [DataRow(CategoryKind.Annotation, "OST_Grids")]
    [DataRow(CategoryKind.Other, "OST_IOSDetailGroups")]
    public void LinkListsModelOnly(CategoryKind kind, string builtIn)
    {
        Assert.IsTrue(CategoryRule.Lists(CategoryKind.Model, true, "OST_Walls", true));
        Assert.IsFalse(CategoryRule.Lists(kind, true, builtIn, true));
    }

    [TestMethod]
    public void AnnotationSubcategoryIsListed()
    {
        Assert.IsTrue(CategoryRule.ListsSubcategory(CategoryKind.Annotation, true, "OST_Dimensions", "OST_DimLeaders"));
    }

    [TestMethod]
    [DataRow(CategoryKind.Annotation, true, "OST_Dimensions", "OST_WeakDims")]
    [DataRow(CategoryKind.Model, false, "OST_Walls", "OST_WallsHidden")]
    [DataRow(CategoryKind.Model, true, "OST_Walls", "INVALID")]
    [DataRow(CategoryKind.Model, true, "OST_DuctCurves", "OST_DuctCurvesCenterLine")]
    [DataRow(CategoryKind.Model, true, "OST_Lines", "OST_LinesHiddenLines")]
    public void SubcategoryFollowsRule(CategoryKind kind, bool visible, string parent, string builtIn)
    {
        Assert.IsFalse(CategoryRule.ListsSubcategory(kind, visible, parent, builtIn));
    }
}
