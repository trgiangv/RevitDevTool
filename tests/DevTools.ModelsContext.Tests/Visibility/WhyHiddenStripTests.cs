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
        await strip.HideDuctsAsync(Current);
        strip.Explainer.Reasons = [HiddenReasons.OutsideCrop, HiddenReasons.Temporary];

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        var view = strip.ViewModel;
        Assert.IsTrue(view.IsOpen);
        Assert.IsFalse(view.IsBusy);
        Assert.AreEqual(HiddenReasons.Temporary.Text, view.Text);
        Assert.IsTrue(view.CanShow);
        Assert.IsTrue(view.HasMore);
        Assert.HasCount(2, view.Reasons);
    }

    [TestMethod]
    public async Task NoCauseShowsNoneFound()
    {
        using var strip = await StripHost.OpenAsync();

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        Assert.AreEqual(HiddenReasons.None.Text, strip.ViewModel.Text);
        Assert.IsFalse(strip.ViewModel.CanShow);
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
        Assert.IsEmpty(strip.ViewModel.Reasons);
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
    public async Task FastAnswerSkipsChecking()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Reasons = [HiddenReasons.Temporary];
        var texts = new List<string>();
        strip.ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WhyHiddenStripViewModel.Text))
            {
                texts.Add(strip.ViewModel.Text);
            }
        };

        await strip.ViewModel.OpenAsync(strip.Duct(Current));
        strip.Clock.Advance(TimeSpan.FromSeconds(1));

        CollectionAssert.AreEqual(new[] { HiddenReasons.Temporary.Text }, texts);
    }

    [TestMethod]
    public async Task SlowAnswerShowsChecking()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Reasons = [HiddenReasons.Temporary];
        var gate = new TaskCompletionSource<bool>();
        strip.Explainer.Gate = gate;
        var open = strip.ViewModel.OpenAsync(strip.Duct(Current));

        strip.Clock.Advance(WhyHiddenStripViewModel.CheckingDelay - TimeSpan.FromMilliseconds(1));
        var early = strip.ViewModel.Text;
        strip.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var late = strip.ViewModel.Text;
        gate.SetResult(true);
        await open;

        Assert.AreEqual(string.Empty, early);
        Assert.AreEqual(WhyHiddenStripViewModel.CheckingText, late);
        Assert.AreEqual(HiddenReasons.Temporary.Text, strip.ViewModel.Text);
    }

    [TestMethod]
    public async Task NewerOpenCancelsOlder()
    {
        using var strip = await StripHost.OpenAsync();
        var first = strip.Duct(Current);
        var second = strip.Duct(Current, 1);
        strip.Explainer.Gate = new TaskCompletionSource<bool>();
        strip.Explainer.ReasonsOf = element => element == first ? [HiddenReasons.OutsideCrop] : [HiddenReasons.InView];
        var older = strip.ViewModel.OpenAsync(first);
        strip.Explainer.Gate = null;

        await strip.ViewModel.OpenAsync(second);
        await older;

        Assert.AreEqual(1, strip.Explainer.Canceled);
        Assert.AreEqual(second, strip.ViewModel.Element);
        Assert.AreEqual(HiddenReasons.InView.Text, strip.ViewModel.Text);
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
        Assert.IsFalse(strip.ViewModel.CanShow);
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
        strip.Explainer.Reasons = [HiddenReasons.OutsideCrop];

        await strip.ViewModel.OpenAsync(strip.Duct(Mep));

        Assert.AreEqual(HiddenReasons.LinkElement, strip.ViewModel.First);
        Assert.HasCount(2, strip.ViewModel.Reasons);
    }

    [TestMethod]
    public async Task FixShowsThenCloses()
    {
        using var strip = await StripHost.OpenAsync();
        await strip.HideDuctsAsync(Current);
        strip.Explainer.ReasonsOf = _ => strip.Explainer.Calls.Count == 1 ? [HiddenReasons.Temporary] : [];
        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        await strip.ViewModel.ShowCommand.ExecuteAsync(null);

        Assert.HasCount(2, strip.Explainer.Calls);
        Assert.AreEqual(1, strip.Actions.ResetCount);
        Assert.IsFalse(strip.ViewModel.IsOpen);
    }

    [TestMethod]
    public async Task FixStillHiddenReExplains()
    {
        using var strip = await StripHost.OpenAsync();
        await strip.HideDuctsAsync(Current);
        strip.Explainer.Reasons = [HiddenReasons.Temporary, HiddenReasons.OutsideCrop];
        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        await strip.ViewModel.ShowCommand.ExecuteAsync(null);

        Assert.HasCount(2, strip.Explainer.Calls);
        Assert.AreEqual(1, strip.Actions.ResetCount);
        Assert.IsTrue(strip.ViewModel.IsOpen);
        Assert.IsFalse(strip.ViewModel.IsBusy);
    }

    [TestMethod]
    public async Task CategoryCueHasNoFix()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Reasons = [HiddenReasons.Category("Ducts")];

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        Assert.IsFalse(strip.ViewModel.CanShow);
    }

    [TestMethod]
    public async Task FixRefusedShowsCue()
    {
        using var strip = await StripHost.OpenAsync();
        await strip.HideDuctsAsync(Current);
        strip.Explainer.Reasons = [HiddenReasons.Temporary];
        await strip.ViewModel.OpenAsync(strip.Duct(Current));
        strip.Actions.ResetError = "Revit is busy.";

        await strip.ViewModel.ShowCommand.ExecuteAsync(null);

        Assert.AreEqual("Revit is busy.", strip.ViewModel.Cue);
        Assert.HasCount(1, strip.Explainer.Calls);
        Assert.IsTrue(strip.ViewModel.IsOpen);
    }

    [TestMethod]
    public async Task FailedResetKeepsPanelHide()
    {
        using var strip = await StripHost.OpenAsync();
        await strip.HideDuctsAsync(Current);
        strip.Explainer.Reasons = [HiddenReasons.Temporary];
        await strip.ViewModel.OpenAsync(strip.Duct(Current));
        strip.Actions.ResetError = "Revit is busy.";
        await strip.ViewModel.ShowCommand.ExecuteAsync(null);

        await strip.ViewModel.OpenAsync(strip.Duct(Current, 1));

        Assert.IsTrue(strip.ViewModel.CanShow);
    }

    [TestMethod]
    public async Task UserHideOffersNoShow()
    {
        using var strip = await StripHost.OpenAsync();
        strip.Explainer.Reasons = [HiddenReasons.Temporary];

        await strip.ViewModel.OpenAsync(strip.Duct(Current));

        Assert.AreEqual(HiddenReasons.Temporary.Text, strip.ViewModel.Text);
        Assert.IsFalse(strip.ViewModel.CanShow);
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

        private StripHost(FakeHost host, FakeClock clock, ModelContextViewModel panel)
        {
            _host = host;
            Clock = clock;
            Panel = panel;
        }

        public FakeClock Clock { get; }

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
            return new StripHost(host, clock, panel);
        }

        public Task HideDuctsAsync(DocumentKey document)
        {
            var ducts = Panel.Explorer.Rows.First(row =>
                row.Kind == TreeRowKind.Group && row.Document == document && row.Text == "Ducts");
            return Panel.Explorer.ToggleEyeCommand.ExecuteAsync(ducts);
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
