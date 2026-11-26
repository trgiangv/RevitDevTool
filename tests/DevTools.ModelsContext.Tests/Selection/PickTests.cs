using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Selection;

[TestClass]
public sealed class PickTests
{
    private static readonly long[] Categories = [-2000011];

    [TestMethod]
    public void NoLinkDisablesPick()
    {
        Assert.AreEqual(PickController.NoLinkReason,
            PickController.DisabledReason([FakeModelGenerator.Current], Categories));
    }

    [TestMethod]
    public void NoCategoryDisablesPick()
    {
        Assert.AreEqual(PickController.NoCategoryReason,
            PickController.DisabledReason([FakeModelGenerator.Mep], []));
    }

    [TestMethod]
    public void LinkEnablesPick()
    {
        Assert.IsNull(PickController.DisabledReason([FakeModelGenerator.Current, FakeModelGenerator.Mep], Categories));
    }

    [TestMethod]
    public async Task PickSendsLinksOnly()
    {
        var model = new FakeModel(TestModel.SmallData);
        var controller = new PickController(model);

        await controller.PickAsync([FakeModelGenerator.Current, FakeModelGenerator.Mep, FakeModelGenerator.Mep],
            [.. Categories, .. Categories], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Mep }, model.LastPickLinks.ToList());
        CollectionAssert.AreEqual(Categories, model.LastPickCategories.ToList());
    }

    [TestMethod]
    public async Task NoLinkSkipsPort()
    {
        var model = new FakeModel(TestModel.SmallData);
        var controller = new PickController(model);

        var result = await controller.PickAsync([FakeModelGenerator.Current], Categories, CancellationToken.None);

        Assert.IsTrue(result.Cancelled);
        Assert.AreEqual(0, model.PickCalls);
    }

    [TestMethod]
    public async Task PickedBecomeSelection()
    {
        var model = new FakeModel(TestModel.SmallData);
        var picked = new ElementRef(FakeModelGenerator.Mep, 7);
        model.PickCandidates = [picked, new ElementRef(FakeModelGenerator.ArchFirst, 8)];
        var controller = new PickController(model);

        var result = await controller.PickAsync([FakeModelGenerator.Mep], Categories, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Picked 1", PickController.StatusOf(result));
        var selection = await model.GetSelectionAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new[] { picked }, selection.ToList());
    }

    [TestMethod]
    public async Task EscapeKeepsSelection()
    {
        var model = new FakeModel(TestModel.SmallData);
        var kept = new ElementRef(FakeModelGenerator.Current, 1);
        await model.SelectAsync([kept], CancellationToken.None);
        model.PickCandidates = [new ElementRef(FakeModelGenerator.Mep, 7)];
        model.CancelPick = true;
        var controller = new PickController(model);

        var result = await controller.PickAsync([FakeModelGenerator.Mep], Categories, CancellationToken.None);

        Assert.AreEqual(PickController.CancelledCue, PickController.StatusOf(result));
        var selection = await model.GetSelectionAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new[] { kept }, selection.ToList());
    }

    [TestMethod]
    public void EmptyPickSaysNothing()
    {
        Assert.AreEqual(PickController.NothingCue, PickController.StatusOf(PickResult.Of([])));
    }

    [TestMethod]
    public void PickCountsEachElementOnce()
    {
        var element = new ElementRef(FakeModelGenerator.Mep, 7);

        var result = PickResult.Of([element, element]);

        Assert.AreEqual("Picked 1", PickController.StatusOf(result));
    }

    [TestMethod]
    public async Task SecondPickIsBlocked()
    {
        var model = new FakeModel(TestModel.SmallData) { PickGate = new TaskCompletionSource<bool>() };
        var controller = new PickController(model);

        var first = controller.PickAsync([FakeModelGenerator.Mep], Categories, CancellationToken.None);
        Assert.IsTrue(controller.IsPicking);
        Assert.IsFalse(controller.CanPick([FakeModelGenerator.Mep], Categories));
        var second = await controller.PickAsync([FakeModelGenerator.Mep], Categories, CancellationToken.None);
        model.PickGate.SetResult(true);
        await first;

        Assert.IsTrue(second.Cancelled);
        Assert.AreEqual(1, model.PickCalls);
        Assert.IsFalse(controller.IsPicking);
    }

    [TestMethod]
    public async Task CloseDuringPickIsQuiet()
    {
        var model = new FakeModel(TestModel.SmallData) { PickGate = new TaskCompletionSource<bool>() };
        var controller = new PickController(model);
        using var close = new CancellationTokenSource();

        var pick = controller.PickAsync([FakeModelGenerator.Mep], Categories, close.Token);
        close.Cancel();
        var result = await pick;

        Assert.IsTrue(result.Cancelled);
        Assert.IsFalse(controller.IsPicking);
    }

    [TestMethod]
    public async Task PickingRaisesChanged()
    {
        var model = new FakeModel(TestModel.SmallData);
        var controller = new PickController(model);
        var states = new List<bool>();
        controller.PickingChanged += (_, _) => states.Add(controller.IsPicking);

        await controller.PickAsync([FakeModelGenerator.Mep], Categories, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { true, false }, states);
    }
}
