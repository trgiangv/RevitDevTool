using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class PruneAndDeltaTests
{
    private static readonly SourceId Arch = FakeModelGenerator.ArchSource;
    private static readonly SourceId Mep = FakeModelGenerator.MepSource;

    [TestMethod]
    public void AnyParentLosingAllIsDropped()
    {
        var scoped = FilterSet.All(new FilterRule(FakeParameters.Comments, FilterOperator.NotEmpty))
            .In(FilterScope.Of([Arch]));
        var root = FilterSet.Any(scoped);

        Assert.IsNull(FilterScopes.ForDocument(root, Mep, new HashSet<SourceId> { Arch, Mep }));
    }

    [TestMethod]
    public void EmptyAnyParentIsKept()
    {
        var root = FilterSet.Any();

        Assert.IsNotNull(FilterScopes.ForDocument(root, Mep, new HashSet<SourceId> { Arch, Mep }));
    }

    [TestMethod]
    public void DeleteThenReAddSameId()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var row = snapshot.Rows[0];
        var readded = new ElementRecord(row.Id, snapshot.Types[row.TypeIndex].TypeId, row.LevelId, row.WorksetId);

        var outcome = DeltaApplier.Apply(snapshot, new ElementDelta(snapshot.Source, [], [readded], [], [row.Id]));

        Assert.AreEqual(DeltaOutcome.Applied, outcome);
        Assert.IsTrue(snapshot.Contains(row.Id));
    }

    [TestMethod]
    public void ModifiedUnknownTypeMarksDirty()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var row = snapshot.Rows[0];
        var modified = new ElementRecord(row.Id, 123_456_789, row.LevelId, row.WorksetId);

        var outcome = DeltaApplier.Apply(snapshot, new ElementDelta(snapshot.Source, [], [], [modified], []));

        Assert.AreEqual(DeltaOutcome.Dirty, outcome);
        Assert.IsTrue(snapshot.IsDirty);
    }
}
