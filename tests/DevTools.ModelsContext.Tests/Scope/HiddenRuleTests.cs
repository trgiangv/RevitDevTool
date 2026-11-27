using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Tests.Scope;

[TestClass]
public sealed class HiddenRuleTests
{
    private const long Shown = 1;
    private const long Hidden = 2;
    private const long Owned = 3;
    private const long OwnedHere = 4;
    private const long NoGeometry = 5;
    private const long Room = 6;
    private const long Area = 7;
    private const long ViewId = 900;
    private const long OtherViewId = 901;
    private static readonly SourceId Source = new("model:rule.rvt");

    [TestMethod]
    public void PlanCountsDrawableOnly()
    {
        var undrawn = Undrawn(DocumentKey.Current("rule"), ViewKind.Plan);

        CollectionAssert.AreEquivalent(new[] { Hidden, OwnedHere, Room }, undrawn.ToList());
    }

    [TestMethod]
    public void LinkSkipsViewOwned()
    {
        var undrawn = Undrawn(DocumentKey.Link("rule", 50), ViewKind.Plan);

        CollectionAssert.AreEquivalent(new[] { Hidden, Room }, undrawn.ToList());
    }

    [TestMethod]
    public void ThreeDSkipsRooms()
    {
        var undrawn = Undrawn(DocumentKey.Link("rule", 50), ViewKind.ThreeD);

        CollectionAssert.AreEquivalent(new[] { Hidden }, undrawn.ToList());
    }

    [TestMethod]
    public void AreaPlanCountsAreas()
    {
        var undrawn = Undrawn(DocumentKey.Link("rule", 50), ViewKind.AreaPlan);

        CollectionAssert.AreEquivalent(new[] { Hidden, Room, Area }, undrawn.ToList());
    }

    [TestMethod]
    public void NonModelViewKeepsOwnedOnly()
    {
        var undrawn = Undrawn(DocumentKey.Current("rule"), ViewKind.Other);

        CollectionAssert.AreEquivalent(new[] { OwnedHere }, undrawn.ToList());
    }

    [TestMethod]
    public void NoViewKeepsModelRows()
    {
        var row = new ElementRow(Hidden, 0, 0, 0, 0);

        Assert.IsTrue(ViewDraws.CanDraw(null, row, true));
        Assert.IsFalse(ViewDraws.CanDraw(null, row with { Draw = ViewDraw.None }, true));
    }

    [TestMethod]
    public void ViewChangeRecountsUndrawn()
    {
        var key = DocumentKey.Current("rule");
        var scope = Scope(key);

        var plan = scope.UndrawnIds(View(key, ViewKind.Plan)).Count;
        var threeD = scope.UndrawnIds(View(key, ViewKind.ThreeD)).Count;

        Assert.AreEqual(3, plan);
        Assert.AreEqual(2, threeD);
    }

    private static HashSet<long> Undrawn(DocumentKey key, ViewKind kind)
    {
        return Scope(key).UndrawnIds(View(key, kind));
    }

    private static ViewInfo View(DocumentKey key, ViewKind kind)
    {
        return new ViewInfo(DocumentKey.Current(key.ModelId), ViewId.ToString(System.Globalization.CultureInfo.InvariantCulture), "L1", kind);
    }

    private static DocumentScope Scope(DocumentKey key)
    {
        var snapshot = new DocumentSnapshotBuilder(Source)
            .AddType(new TypeRecord(10, -2000011, "Walls", "Basic Wall", "Generic"))
            .AddElement(new ElementRecord(Shown, 10, 0, 0))
            .AddElement(new ElementRecord(Hidden, 10, 0, 0))
            .AddElement(new ElementRecord(Owned, 10, 0, 0, false, ViewDraw.None, OtherViewId))
            .AddElement(new ElementRecord(OwnedHere, 10, 0, 0, false, ViewDraw.None, ViewId))
            .AddElement(new ElementRecord(NoGeometry, 10, 0, 0, false, ViewDraw.None))
            .AddElement(new ElementRecord(Room, 10, 0, 0, false, ViewDraw.Spatial))
            .AddElement(new ElementRecord(Area, 10, 0, 0, false, ViewDraw.AreaPlan))
            .Build();
        var info = new DocumentInfo(key, Source, "rule", true, DocumentCapabilities.LinkInstance);
        return new DocumentScope(info, "rule", snapshot, new HashSet<long> { Shown });
    }
}
