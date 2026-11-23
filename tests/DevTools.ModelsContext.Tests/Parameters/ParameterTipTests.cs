using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Tests.Parameters;

[TestClass]
public sealed class ParameterTipTests
{
    private static readonly Guid SharedId = new("a3f2b1c0-1111-2222-3333-44445555c941");

    [TestMethod]
    public void BuiltInMeasuredShowsDiscipline()
    {
        var tip = ParameterTip.Of(new ParameterInfo(ParameterKey.BuiltIn("Width", "RBS_CURVE_WIDTH_PARAM"), "Width",
            ValueKind.Number, ParameterStorage.Double, "spec:duct", "Duct Size", ParameterOrigin.BuiltIn)
        {
            Discipline = "HVAC",
            Group = "Dimensions",
            Models = ["Current model", "Plumbing"],
            CategoryNames = ["Ducts"]
        });

        CollectionAssert.AreEqual(new[]
        {
            new TipRow(ParameterTip.BuiltInLabel, "RBS_CURVE_WIDTH_PARAM"),
            new TipRow(ParameterTip.DisciplineLabel, "HVAC"),
            new TipRow(ParameterTip.TypeLabel, "Duct Size"),
            new TipRow(ParameterTip.GroupLabel, "Dimensions")
        }, Rows(tip));
        CollectionAssert.AreEqual(new[] { "Current model", "Plumbing" }, tip.Models.Items.ToArray());
        Assert.AreEqual(ParameterTip.CategoriesTitle, tip.Places.Title);
    }

    [TestMethod]
    public void BuiltInTextHasNoDiscipline()
    {
        var tip = ParameterTip.Of(new ParameterInfo(ParameterKey.BuiltIn("Comments", "ALL_MODEL_INSTANCE_COMMENTS"),
            "Comments", ValueKind.Text, ParameterStorage.String, "spec:text", "Text", ParameterOrigin.BuiltIn)
        {
            Group = "Identity Data"
        });

        CollectionAssert.AreEqual(new[] { ParameterTip.BuiltInLabel, ParameterTip.TypeLabel, ParameterTip.GroupLabel },
            Labels(tip));
    }

    [TestMethod]
    public void SharedShowsShortGuid()
    {
        var tip = ParameterTip.Of(new ParameterInfo(ParameterKey.Shared("Fire Rating", SharedId), "Fire Rating",
            ValueKind.Text, ParameterStorage.String, "spec:text", "Text", ParameterOrigin.Shared));

        Assert.AreEqual(new TipRow(ParameterTip.GuidLabel, "a3f2…c941"), Rows(tip)[0]);
        CollectionAssert.DoesNotContain(Labels(tip), ParameterTip.BuiltInLabel);
    }

    [TestMethod]
    public void ProjectHasNoIdRow()
    {
        var tip = ParameterTip.Of(new ParameterInfo(ParameterKey.Named("Zone Code", "spec:text"), "Zone Code",
            ValueKind.Text, ParameterStorage.String, "spec:text", "Text", ParameterOrigin.Project)
        {
            CategoryNames = ["Air Terminals", "Ducts"]
        });

        CollectionAssert.AreEqual(new[] { ParameterTip.TypeLabel }, Labels(tip));
        Assert.AreEqual(ParameterTip.CategoriesTitle, tip.Places.Title);
    }

    [TestMethod]
    public void FamilyListsFamilies()
    {
        var tip = ParameterTip.Of(new ParameterInfo(ParameterKey.Named("Width", "spec:length"), "Width",
            ValueKind.Number, ParameterStorage.Double, "spec:length", "Length", ParameterOrigin.Family)
        {
            Discipline = "Common",
            Families = ["F7", "F2", "F1", "F3", "F6", "F4", "F5"],
            CategoryNames = ["Air Terminals"]
        });

        Assert.AreEqual(ParameterTip.FamiliesTitle, tip.Places.Title);
        CollectionAssert.AreEqual(new[] { "F1", "F2", "F3", "F4", "F5" }, tip.Places.Items.ToArray());
        Assert.AreEqual("+ 2 more", tip.Places.More);
    }

    [TestMethod]
    public void YesNoShowsTypeAndGroup()
    {
        var tip = ParameterTip.Of(new ParameterInfo(ParameterKey.BuiltIn("Room Bounding", "WALL_ATTR_ROOM_BOUNDING"),
            "Room Bounding", ValueKind.YesNo, ParameterStorage.Integer, "spec:yesno", "Yes/No", ParameterOrigin.BuiltIn)
        {
            Group = "Constraints"
        });

        CollectionAssert.AreEqual(new[] { ParameterTip.BuiltInLabel, ParameterTip.TypeLabel, ParameterTip.GroupLabel },
            Labels(tip));
    }

    [TestMethod]
    public void UnknownGroupIsHidden()
    {
        var tip = ParameterTip.Of(new ParameterInfo(ParameterKey.BuiltIn("Mark", "ALL_MODEL_MARK"), "Mark",
            ValueKind.Text, SpecLabel: "Text"));

        CollectionAssert.DoesNotContain(Labels(tip), ParameterTip.GroupLabel);
    }

    [TestMethod]
    public void SummaryShowsTwoThenCount()
    {
        Assert.AreEqual("Doors, Windows +2", ParameterInfo.Summary(["Doors", "Windows", "Furniture", "Casework"]));
        Assert.AreEqual("Ducts", ParameterInfo.Summary(["Ducts"]));
    }

    [TestMethod]
    [DataRow(ParameterOrigin.BuiltIn, ParameterOrigin.BuiltIn)]
    [DataRow(ParameterOrigin.Family | ParameterOrigin.Shared, ParameterOrigin.Shared)]
    [DataRow(ParameterOrigin.Family | ParameterOrigin.Project, ParameterOrigin.Project)]
    [DataRow(ParameterOrigin.Family, ParameterOrigin.Family)]
    public void SourceIconPerOrigin(ParameterOrigin origins, ParameterOrigin expected)
    {
        Assert.AreEqual(expected, new ParameterInfo(ParameterKey.Named("X", null), "X", ValueKind.Text, Origins: origins).Source);
    }

    [TestMethod]
    [DataRow(ParameterBinding.Instance, ParameterBinding.Instance)]
    [DataRow(ParameterBinding.Instance | ParameterBinding.Type, ParameterBinding.Instance)]
    [DataRow(ParameterBinding.Type, ParameterBinding.Type)]
    [DataRow(ParameterBinding.None, ParameterBinding.Instance)]
    public void SectionFollowsBinding(ParameterBinding bindings, ParameterBinding expected)
    {
        Assert.AreEqual(expected, new ParameterInfo(ParameterKey.Named("X", null), "X", ValueKind.Text, Bindings: bindings).Section);
    }

    private static TipRow[] Rows(ParameterTip tip)
    {
        return tip.Rows.ToArray();
    }

    private static string[] Labels(ParameterTip tip)
    {
        return tip.Rows.Select(row => row.Label).ToArray();
    }
}
