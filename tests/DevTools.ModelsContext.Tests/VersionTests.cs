using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class VersionTests
{
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;
    private static readonly SourceId MepSource = FakeModelGenerator.MepSource;

    [TestMethod]
    public async Task SameLinkVersionKeepsSnapshot()
    {
        var (model, store) = Open();
        var snapshot = await store.GetAsync(Mep, null, CancellationToken.None);

        var outcome = await store.ReloadAsync(MepSource, CancellationToken.None);

        Assert.AreEqual(ReloadOutcome.Kept, outcome);
        Assert.AreSame(snapshot, store.TryGet(Mep));
        Assert.AreEqual(1, model.BuildCount);
    }

    [TestMethod]
    public async Task ChangedVersionAppliesDelta()
    {
        var (model, store) = Open();
        var snapshot = await store.GetAsync(Mep, null, CancellationToken.None);
        var before = snapshot.ElementCount;
        var elements = model.SourceOf(Mep).Elements;
        var added = new ElementRecord(9_999_999, elements[10].TypeId, 300, 1);

        var version = model.SaveVersion(MepSource, [added], elements.Take(5).Select(element => element.Id).ToList());
        var outcome = await store.ReloadAsync(MepSource, CancellationToken.None);

        Assert.AreEqual(ReloadOutcome.Patched, outcome);
        Assert.AreSame(snapshot, store.TryGet(Mep));
        Assert.AreEqual(before - 4, snapshot.ElementCount);
        Assert.IsTrue(snapshot.Contains(added.Id));
        Assert.AreEqual(version, snapshot.Stamp);
        Assert.AreEqual(1, model.BuildCount);
    }

    [TestMethod]
    public async Task UnknownVersionRebuilds()
    {
        var (model, store) = Open();
        var first = await store.GetAsync(Mep, null, CancellationToken.None);
        model.SaveVersion(MepSource, deleted: [model.SourceOf(Mep).Elements[0].Id]);
        model.ForgetHistory(MepSource);

        var outcome = await store.ReloadAsync(MepSource, CancellationToken.None);
        var second = await store.GetAsync(Mep, null, CancellationToken.None);

        Assert.AreEqual(ReloadOutcome.Rebuilt, outcome);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(2, model.BuildCount);
    }

    [TestMethod]
    public async Task LargeDeltaRebuilds()
    {
        var (model, store) = Open();
        var snapshot = await store.GetAsync(Mep, null, CancellationToken.None);
        var deleted = model.SourceOf(Mep).Elements.Take(snapshot.ElementCount / 4).Select(element => element.Id)
            .ToList();
        model.SaveVersion(MepSource, deleted: deleted);

        var outcome = await store.ReloadAsync(MepSource, CancellationToken.None);

        Assert.AreEqual(ReloadOutcome.Rebuilt, outcome);
        Assert.IsTrue(snapshot.IsDirty);
    }

    [TestMethod]
    public async Task MissedChangeMarksDirty()
    {
        var (model, store) = Open();
        var snapshot = await store.GetAsync(FakeModelGenerator.Current, null, CancellationToken.None);
        model.SaveVersion(FakeModelGenerator.CurrentSource,
            deleted: [model.SourceOf(FakeModelGenerator.Current).Elements[0].Id]);

        var consistent = await store.VerifyAsync(FakeModelGenerator.CurrentSource, CancellationToken.None);

        Assert.IsFalse(consistent);
        Assert.IsTrue(snapshot.IsDirty);
    }

    [TestMethod]
    public async Task AppliedChangePassesCheck()
    {
        var (model, store) = Open();
        var snapshot = await store.GetAsync(FakeModelGenerator.Current, null, CancellationToken.None);
        var id = model.SourceOf(FakeModelGenerator.Current).Elements[0].Id;
        var version = model.SaveVersion(FakeModelGenerator.CurrentSource, deleted: [id]);
        store.Apply(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, [id]));

        var consistent = await store.VerifyAsync(FakeModelGenerator.CurrentSource, CancellationToken.None);

        Assert.IsTrue(consistent);
        Assert.IsFalse(snapshot.IsDirty);
        Assert.AreEqual(version, snapshot.Stamp);
    }

    private static (FakeModel Model, SnapshotStore Store) Open()
    {
        var model = new FakeModel(TestModel.SmallData);
        var store = new SnapshotStore(model);
        store.SetDocuments(model.Data.Documents);
        return (model, store);
    }
}
