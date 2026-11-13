using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class DeltaTests
{
    [TestMethod]
    public void DeleteDecrementsChain()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var row = snapshot.Rows[10];
        var path = new int[3];
        snapshot.PathOf(row, path);
        var before = Enumerable.Range(0, 4).Select(depth => snapshot.Counts.CountAt(path.Take(depth).ToArray())).ToList();

        var outcome = DeltaApplier.Apply(snapshot, ElementDelta.DeletedOnly(snapshot.Source, [row.Id]));

        Assert.AreEqual(DeltaOutcome.Applied, outcome);
        for (var depth = 0; depth < 4; depth++)
        {
            Assert.AreEqual(before[depth] - 1, snapshot.Counts.CountAt(path.Take(depth).ToArray()));
        }

        Assert.IsFalse(snapshot.Contains(row.Id));
    }

    [TestMethod]
    public void DeletesKeepOtherIdsFound()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var ids = snapshot.Rows.Select(row => row.Id).ToList();
        var deleted = ids.Where((_, index) => index % 7 == 3).Take(DeltaApplier.DirtyThreshold).ToList();

        DeltaApplier.Apply(snapshot, ElementDelta.DeletedOnly(snapshot.Source, deleted));

        var gone = deleted.ToHashSet();
        Assert.IsTrue(deleted.All(id => !snapshot.Contains(id)));
        foreach (var id in ids.Where(id => !gone.Contains(id)))
        {
            Assert.IsTrue(snapshot.TryGetRow(id, out var row));
            Assert.AreEqual(id, row.Id);
        }
    }

    [TestMethod]
    public void LargeDeltaMarksDirty()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var ids = snapshot.Rows.Take(DeltaApplier.DirtyThreshold + 1).Select(row => row.Id).ToList();

        var outcome = DeltaApplier.Apply(snapshot, ElementDelta.DeletedOnly(snapshot.Source, ids));

        Assert.AreEqual(DeltaOutcome.Dirty, outcome);
        Assert.IsTrue(snapshot.IsDirty);
    }

    [TestMethod]
    public void AddedElementIsCounted()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var type = snapshot.Types[0];
        var group = type.GroupIndex;
        var before = snapshot.Counts.CountAt(group);
        var added = new ElementRecord(42, type.TypeId, 300, 1);

        DeltaApplier.Apply(snapshot, new ElementDelta(snapshot.Source, [], [added], [], []));

        Assert.AreEqual(before + 1, snapshot.Counts.CountAt(group));
        Assert.IsTrue(snapshot.Contains(42));
    }

    [TestMethod]
    public void TypeChangeMovesCount()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var row = snapshot.Rows[0];
        var target = snapshot.Types.First(type => type.GroupIndex != row.GroupIndex);
        var oldBefore = snapshot.Counts.CountAt(row.GroupIndex);
        var newBefore = snapshot.Counts.CountAt(target.GroupIndex);
        var modified = new ElementRecord(row.Id, target.TypeId, row.LevelId, row.WorksetId);

        DeltaApplier.Apply(snapshot, new ElementDelta(snapshot.Source, [], [], [modified], []));

        Assert.AreEqual(oldBefore - 1, snapshot.Counts.CountAt(row.GroupIndex));
        Assert.AreEqual(newBefore + 1, snapshot.Counts.CountAt(target.GroupIndex));
    }

    [TestMethod]
    public void UnknownTypeMarksDirty()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var added = new ElementRecord(42, 123_456_789, 300, 1);

        var outcome = DeltaApplier.Apply(snapshot, new ElementDelta(snapshot.Source, [], [added], [], []));

        Assert.AreEqual(DeltaOutcome.Dirty, outcome);
    }
}
