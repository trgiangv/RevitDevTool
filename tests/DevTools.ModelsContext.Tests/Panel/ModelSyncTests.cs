using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Panel;

[TestClass]
public sealed class ModelSyncTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task SyncKeepsReflectedSnapshot()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var builds = fixture.Model.BuildCount;

        fixture.Model.RaiseModelSynced(Current);
        await SettleAsync(fixture);

        Assert.AreEqual(builds, fixture.Model.BuildCount);
    }

    [TestMethod]
    public async Task SyncRebuildsMissedChange()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var builds = fixture.Model.BuildCount;
        var before = fixture.Explorer.Rows[0].Count;
        var source = fixture.Model.Data.SourceOf(Current);
        var template = source.Elements[0];
        fixture.Model.SaveVersion(FakeModelGenerator.CurrentSource,
            added: [template with { Id = source.Elements.Max(element => element.Id) + 1 }]);

        fixture.Model.RaiseModelSynced(Current);
        await SettleAsync(fixture);

        Assert.IsGreaterThan(builds, fixture.Model.BuildCount);
        Assert.AreEqual(before + 1, fixture.Explorer.Rows[0].Count);
    }

    [TestMethod]
    public async Task SyncOfOtherModelIgnored()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var builds = fixture.Model.BuildCount;

        fixture.Model.RaiseModelSynced(DocumentKey.Current("other-model"));
        await SettleAsync(fixture);

        Assert.AreEqual(builds, fixture.Model.BuildCount);
    }

    private static async Task SettleAsync(PanelFixture fixture)
    {
        for (var turn = 0; turn < 5; turn++)
        {
            fixture.Settle();
            await Task.Yield();
        }
    }
}
