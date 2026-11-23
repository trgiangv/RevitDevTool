using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class WhyHiddenStripTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task OpenShowsFirstCause()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Causes = [HiddenCues.OutsideCrop, HiddenCues.TemporaryHideIsolate];

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        var view = strip.ViewModel;
        Assert.IsTrue(view.IsOpen);
        Assert.IsFalse(view.IsBusy);
        Assert.AreEqual(HiddenCues.TemporaryHideIsolate.Text, view.Text);
        Assert.IsTrue(view.HasFix);
        Assert.IsTrue(view.HasMore);
        Assert.HasCount(2, view.Causes);
    }

    [TestMethod]
    public async Task NoCauseShowsNoneFound()
    {
        using var strip = await StripHost.OpenAsync();

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        Assert.AreEqual(HiddenCues.NoneFound.Text, strip.ViewModel.Text);
        Assert.IsFalse(strip.ViewModel.HasFix);
        Assert.IsFalse(strip.ViewModel.HasMore);
    }

    [TestMethod]
    public async Task FollowSwitchesElement()
    {
        using var strip = await StripHost.OpenAsync();
        var first = strip.Duct(Current);
        var second = strip.Duct(Current, 1);
        await strip.ViewModel.OpenAsync(first);

        await strip.ViewModel.FollowAsync(second);
        await strip.ViewModel.FollowAsync(second);

        Assert.AreEqual(second, strip.ViewModel.Element);
        CollectionAssert.AreEqual(new[] { first, second }, strip.Explainer.Calls);
    }

    [TestMethod]
    public async Task FollowOpensWhenClosed()
    {
        using var strip = await StripHost.OpenAsync();

        await strip.ViewModel.FollowAsync(strip.Duct(Current));

        Assert.IsTrue(strip.ViewModel.IsOpen);
    }

    [TestMethod]
    public async Task FollowWithoutRowCloses()
    {
        using var strip = await StripHost.OpenAsync();
        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        await strip.ViewModel.FollowAsync(null);

        Assert.IsFalse(strip.ViewModel.IsOpen);
        Assert.IsNull(strip.ViewModel.Element);
        Assert.IsEmpty(strip.ViewModel.Causes);
    }

    [TestMethod]
    public async Task CloseCommandCloses()
    {
        using var strip = await StripHost.OpenAsync();
        var closedBefore = strip.ViewModel.CloseCommand.CanExecute(null);
        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        strip.ViewModel.CloseCommand.Execute(null);

        Assert.IsFalse(closedBefore);
        Assert.IsFalse(strip.ViewModel.IsOpen);
        Assert.IsFalse(strip.ViewModel.CloseCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task NewerOpenCancelsOlder()
    {
        using var strip = await StripHost.OpenAsync();
        var first = strip.Duct(Current);
        var second = strip.Duct(Current, 1);
        strip.Explainer.Gate = new TaskCompletionSource<bool>();
        strip.Explainer.CausesOf = element => element == first ? [HiddenCues.OutsideCrop] : [HiddenCues.HiddenInView];
        var older = strip.ViewModel.OpenAsync(first);
        strip.Explainer.Gate = null;

        await strip.ViewModel.OpenAsync(second);
        await older;

        Assert.AreEqual(1, strip.Explainer.Canceled);
        Assert.AreEqual(second, strip.ViewModel.Element);
        Assert.AreEqual(HiddenCues.HiddenInView.Text, strip.ViewModel.Text);
    }

    [TestMethod]
    public async Task ErrorBecomesCue()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Error = new InvalidOperationException("Element gone.");

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        Assert.IsTrue(strip.ViewModel.IsOpen);
        Assert.AreEqual(WhyHiddenStripViewModel.FailedText, strip.ViewModel.Text);
        Assert.AreEqual("Element gone.", strip.ViewModel.Cue);
        Assert.IsFalse(strip.ViewModel.HasFix);
        Assert.IsNull(strip.Panel.Message);
    }

    [TestMethod]
    public async Task JournalHideComesFirst()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Panel.ContextBar.Tick([Mep]);
        var ducts = strip.Panel.Explorer.Rows.First(row =>
            row.Kind == TreeRowKind.Group && row.Document == Mep && row.Text == "Ducts");
        await strip.Panel.Explorer.ToggleEyeCommand.ExecuteAsync(ducts);
        strip.Explainer.Causes = [HiddenCues.OutsideCrop];

        await strip.ViewModel.OpenAsync(strip.Duct(Mep));

        Assert.AreEqual(HiddenCues.HiddenInLink, strip.ViewModel.First);
        Assert.HasCount(2, strip.ViewModel.Causes);
    }

    [TestMethod]
    public async Task FixShowsThenCloses()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.CausesOf = _ => strip.Explainer.Calls.Count == 1 ? [HiddenCues.TemporaryHideIsolate] : [];
        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        await strip.ViewModel.FixCommand.ExecuteAsync(null);

        Assert.HasCount(2, strip.Explainer.Calls);
        Assert.AreEqual(1, strip.Actions.ResetCount);
        Assert.IsFalse(strip.ViewModel.IsOpen);
    }

    [TestMethod]
    public async Task FixStillHiddenReExplains()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Causes = [HiddenCues.TemporaryHideIsolate, HiddenCues.OutsideCrop];
        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        await strip.ViewModel.FixCommand.ExecuteAsync(null);

        Assert.HasCount(2, strip.Explainer.Calls);
        Assert.AreEqual(1, strip.Actions.ResetCount);
        Assert.IsTrue(strip.ViewModel.IsOpen);
        Assert.IsFalse(strip.ViewModel.IsBusy);
    }

    [TestMethod]
    public async Task CategoryCueHasNoFix()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Causes = [HiddenCues.Category("Ducts")];

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        Assert.IsFalse(strip.ViewModel.HasFix);
    }

    [TestMethod]
    public async Task FixRefusedShowsCue()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Panel.ContextBar.Tick([Mep]);
        strip.Explainer.Causes = [HiddenCues.HiddenInLink];
        await strip.ViewModel.OpenAsync(strip.Duct(Mep));

        await strip.ViewModel.FixCommand.ExecuteAsync(null);

        Assert.AreEqual(VisibilityReasons.Nothing, strip.ViewModel.Cue);
        Assert.HasCount(1, strip.Explainer.Calls);
        Assert.IsTrue(strip.ViewModel.IsOpen);
    }

    [TestMethod]
    public async Task MenuNeedsOneElement()
    {
        using var strip = await StripHost.OpenAsync();
        var ducts = strip.Panel.Explorer.Rows.First(row =>
            row.Kind == TreeRowKind.Group && row.Document == Current && row.Text == "Ducts");

        strip.Panel.Explorer.OpenMenu(ducts);

        Assert.AreEqual(ContextMenuViewModel.NeedsOneElement,
            strip.Panel.ContextMenu[MenuAction.WhyHidden].DisabledReason);
    }

    [TestMethod]
    public async Task MenuOpensStripForInstance()
    {
        using var strip = await StripHost.OpenAsync();
        var instance = await strip.InstanceRowAsync();
        strip.Panel.Explorer.OpenMenu(instance);
        var item = strip.Panel.ContextMenu[MenuAction.WhyHidden];

        await item.Command.ExecuteAsync(null);

        Assert.IsTrue(item.IsEnabled);
        Assert.IsTrue(strip.ViewModel.IsOpen);
        Assert.AreEqual(new ElementRef(Current, instance.Id), strip.ViewModel.Element);
    }

    private sealed class StripHost : IDisposable
    {
        private readonly FakeHost _host;

        private StripHost(FakeHost host, ModelContextViewModel panel)
        {
            _host = host;
            Panel = panel;
        }

        public ModelContextViewModel Panel { get; }

        public WhyHiddenStripViewModel ViewModel => Panel.WhyHidden;

        public FakeVisibilityExplainer Explainer => _host.Explainer;

        public FakeViewActions Actions => _host.Actions;

        public static async Task<StripHost> OpenAsync()
        {
            var host = new FakeHost(new FakeModel(TestModel.SmallData));
            var clock = new FakeClock();
            var panel = new ModelContextViewModel(host.Services(clock, clock));
            await panel.InitializeAsync(CancellationToken.None);
            return new StripHost(host, panel);
        }

        public ElementRef Duct(DocumentKey document, int index = 0)
        {
            var group = Panel.Explorer.Rows.First(row =>
                row.Kind == TreeRowKind.Group && row.Document == document && row.Text == "Ducts");
            return Panel.Explorer.ElementsOf([group]).OrderBy(element => element.Id).ElementAt(index);
        }

        public async Task<TreeRow> InstanceRowAsync()
        {
            var ducts = Panel.Explorer.Rows.First(row =>
                row.Kind == TreeRowKind.Group && row.Document == Current && row.Text == "Ducts");
            await Panel.Explorer.ExpandAsync(ducts);
            while (!Panel.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Instance))
            {
                await Panel.Explorer.ExpandAsync(Panel.Explorer.Rows.First(row =>
                    row.HasChildren && !row.IsExpanded && row.Level > ducts.Level));
            }

            return Panel.Explorer.Rows.First(row => row.Kind == TreeRowKind.Instance);
        }

        public void Dispose()
        {
            Panel.Dispose();
        }
    }
}
