using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Visibility;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using RevitDevTool.ModelsContext.LiveTests;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RevitDevTool.ModelsContext.UiTests;

[TestClass]
[DoNotParallelize]
public sealed class BoxAndBarTests : RecordedTest
{
    private const string Parent = "Toposolid";
    private const string Child = "Sub-divisions";
    private const int ParentCount = 10;
    private const int ChildCount = 8;
    private const int DotMinimum = 10;

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private static readonly string[] Tools = ["Filter.Result.Isolate", "Filter.Result.Halftone", "Filter.Result.Color", "Filter.Result.Eye"];

    private static RevitPanel _panel = null!;
    private static PanelSteps _steps = null!;

    [ClassInitialize]
    public static void Open(TestContext context)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        Evidence.Layer = "L3";
        _panel = RevitPanel.Attach(CaseFiles.Facts.HostTitle);
        _steps = new PanelSteps(_panel);
        _panel.Settle();
        _panel.Select("ModelContext.Scope.WholeModel");
        _panel.Select("ModelContext.View.Explorer");
        _steps.CheckOnly(CaseFiles.Facts.LinkLabel);
    }

    [ClassCleanup]
    public static void Close()
    {
        _panel?.Dispose();
    }

    [TestMethod]
    public void BoxTreeChecksChildren()
    {
        Record(CaseFiles.Flow("UI-09"), () =>
        {
            WithSite(() =>
            {
                OpenBoxWithNone();
                _panel.Shot("before");

                _panel.Toggle(Named("Explorer.Category.Item", Parent), true, false);
                _panel.Toggle(Named("Explorer.Category.Expand", Parent), true, false);
                Evidence.Step($"Checked {Parent} and opened its chevron (Toggle pattern).");
                var childOn = WaitBox(Child, ToggleState.On);
                _panel.Shot("parent");

                _panel.Toggle(Named("Explorer.Category.Item", Child), false, false);
                Evidence.Step($"Unchecked {Child}.");
                var cleared = WaitBox(Parent, ToggleState.Off);
                _panel.Shot("cleared");

                _panel.Toggle(Named("Explorer.Category.Item", Child), true, false);
                Evidence.Step($"Checked {Child} again.");
                var full = WaitBox(Parent, ToggleState.On);
                _panel.Shot("after");
                Evidence.Values($"{Child} On; {Parent} Off; then {Parent} On",
                    $"{Child} {Box(Child)}; {Parent} {(cleared ? "Off" : "not cleared")}; then {Parent} {Box(Parent)}");

                Assert.IsTrue(childOn, $"Checking {Parent} did not check {Child}.");
                Assert.IsTrue(cleared, $"Unchecking {Child}, its only child, did not clear {Parent}.");
                Assert.IsTrue(full, $"Checking every child did not check {Parent}.");
            });
        });
    }

    [TestMethod]
    public void BoxSearchOpensParent()
    {
        Record(CaseFiles.Flow("UI-10"), () =>
        {
            WithSite(() =>
            {
                OpenBoxWithNone();
                _panel.Toggle(Named("Explorer.Category.Expand", Parent), false, false);
                _panel.Shot("before");

                _panel.SetValue(_panel.Find("Explorer.CategorySearch"), "Sub-div", false);
                Evidence.Step("Typed 'Sub-div' in the category search (Value pattern).");
                var listed = Retry.WhileFalse(() => RowNames().SequenceEqual([Parent, Child]), Wait, ignoreException: true).Result;
                var rows = RowNames();
                var open = Named("Explorer.Category.Expand", Parent).Patterns.Toggle.Pattern.ToggleState.Value;
                _panel.Shot("after");
                Evidence.Values($"rows {Parent}, {Child}; chevron On", $"rows {string.Join(", ", rows)}; chevron {open}");

                Assert.IsTrue(listed, "The search did not list the parent and its child: " + string.Join(", ", rows));
                Assert.AreEqual(ToggleState.On, open, "The search did not open the parent.");
            });
        });
    }

    [TestMethod]
    public void BoxRowsSelectMany()
    {
        Record(CaseFiles.Flow("UI-11"), () =>
        {
            WithBox(() =>
            {
                OpenBoxWithNone();
                var walls = CaseFiles.Facts.CategoryNames["OST_Walls"];
                var doors = CaseFiles.Facts.CategoryNames["OST_Doors"];
                _panel.Shot("before");

                Named("Explorer.Category.Row", walls).Patterns.SelectionItem.Pattern.Select();
                Named("Explorer.Category.Row", doors).Patterns.SelectionItem.Pattern.AddToSelection();
                Evidence.Step($"Selected {walls}, then added {doors} (SelectionItem Select, AddToSelection).");
                Retry.WhileFalse(() => Checked().Count == 2, Wait, ignoreException: true);
                var checkedNames = Checked();
                _panel.Shot("after");
                Evidence.Values($"checked {walls}, {doors}", "checked " + string.Join(", ", checkedNames));

                CollectionAssert.AreEquivalent(new[] { walls, doors }, checkedNames.ToList());
            });
        });
    }

    [TestMethod]
    public void SiteNestsSubdivisions()
    {
        Record(CaseFiles.Flow("UI-12"), () =>
        {
            WithSite(() =>
            {
                OpenBoxWithNone();
                _panel.Toggle(Named("Explorer.Category.Item", Parent), true, false);
                _panel.Toggle(_panel.Find("Explorer.Categories"), false, false);
                _panel.Settle();
                var site = _steps.DocumentName("Site");
                _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
                Evidence.Step($"Checked only {Parent} in the box, collapsed all rows.");
                _panel.Shot("before");
                var siteRow = _panel.Item(_panel.Find("Explorer.Rows"), cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(site)));
                _panel.Item(siteRow, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
                var parent = RowUnder(site, Parent);
                _panel.Item(parent, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
                Evidence.Step($"Expanded '{site}', then {Parent}.");
                var child = RowUnder(site, Child);
                var parentCount = RowCount(parent);
                var childCount = RowCount(child);
                _panel.Shot("after");
                Evidence.Values($"{Parent} {ParentCount}, {Child} {ChildCount}", $"{Parent} {parentCount}, {Child} {childCount}");

                Assert.AreEqual(ParentCount, parentCount);
                Assert.AreEqual(ChildCount, childCount);
            });
        });
    }

    [TestMethod]
    public void BoxEyeHidesLinkWalls()
    {
        Record(CaseFiles.Flow("UI-13"), () =>
        {
            var walls = CaseFiles.Facts.CategoryNames["OST_Walls"];
            WithBox(() =>
            {
                _panel.Toggle(_panel.Find("Explorer.Categories"), true, false);
                CheckAllBoxes();
                _panel.Shot("before");
                var stale = _panel.Message();

                _panel.Act(() => Named("Explorer.CategoryEye", walls).Patterns.Invoke.Pattern.Invoke());
                Evidence.Step($"Invoked the box eye on {walls} with only the Architectural link checked.");
                var hidden = Named("Explorer.CategoryEye", walls).Properties.ItemStatus.ValueOrDefault;
                var bar = _panel.Shown("Temporary.Text")?.Name ?? string.Empty;
                var message = _panel.NewMessage(stale);
                _panel.Shot("after");

                _panel.Invoke("Temporary.Reset");
                Evidence.Step("Invoked Temporary.Reset.");
                var gone = _panel.Gone("Temporary.Text");
                Retry.WhileFalse(() => Named("Explorer.CategoryEye", walls).Properties.ItemStatus.ValueOrDefault == "Shown", Wait, ignoreException: true);
                var shown = Named("Explorer.CategoryEye", walls).Properties.ItemStatus.ValueOrDefault;
                _panel.Shot("reset");
                Evidence.Values("eye Hidden, bar with a count; then eye Shown, no bar",
                    $"eye {hidden}, '{bar}', message '{message}'; then eye {shown}, {(gone ? "no bar" : "bar still shown")}");

                Assert.AreEqual("Hidden", hidden);
                Assert.IsTrue(bar.Any(char.IsDigit), $"The temporary bar shows no change count: '{bar}'.");
                Assert.AreEqual("Shown", shown);
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }, ResetLeftovers);
        });
    }

    [TestMethod]
    public void ResultBarKeepsSync()
    {
        Record(CaseFiles.Flow("UI-14"), () =>
        {
            _panel.Select("ModelContext.View.Filter");
            try
            {
                _steps.NewFilter([]);
                _panel.Toggle(_panel.Find("Filter.Categories"), true, false);
                _panel.Invoke("Filter.CategoryAll");
                Evidence.Step("New filter, Categories All.");
                _panel.Shot("before");
                var count = _panel.Count();
                var bar = _panel.Find("Filter.ResultTools").BoundingRectangle;
                var sync = _panel.Find("Filter.SelectionSync").BoundingRectangle;
                var line = new[] { "Filter.ResultCount", "Filter.Mode", "Filter.SelectionSync" }.Select(id => _panel.Find(id).BoundingRectangle).ToList();
                var arrow = _panel.Root.FindAllDescendants(cf => cf.ByAutomationId("ButtonOverflow"))
                    .FirstOrDefault(element => !element.IsOffscreen && element.BoundingRectangle.Width > 0)?.BoundingRectangle;
                var shown = Tools.Where(id => _panel.Shown(id) is { } tool && Inside(tool.BoundingRectangle, bar)).ToList();
                _panel.Shot("after");
                Evidence.Values("one line; sync toggle inside the bar, left of the overflow arrow; every tool in the bar or behind the arrow",
                    $"count {count}; bar {bar}; sync {sync}; arrow {arrow?.ToString() ?? "none"}; tools in bar {string.Join(", ", shown)}");

                Assert.IsLessThan(line.Min(box => box.Bottom), line.Max(box => box.Top), "Count, mode and sync spread over more than one line.");
                Assert.IsGreaterThanOrEqualTo(20, sync.Width, $"The sync toggle is clipped: {sync}.");
                Assert.IsTrue(Inside(sync, bar), $"The sync toggle {sync} is not inside the bar {bar}.");
                Assert.IsTrue(arrow is not { } over || sync.Right <= over.Left + 1, $"The sync toggle {sync} runs under the overflow arrow {arrow}.");
                Assert.IsTrue(shown.Count == Tools.Length || arrow is not null, "A tool left the bar with no overflow arrow.");
            }
            finally
            {
                _panel.Invoke("Filter.CategoryNone");
                _panel.Select("ModelContext.View.Explorer");
            }
        });
    }

    [TestMethod]
    public void SyncTogglesAreSeparate()
    {
        Record(CaseFiles.Flow("UI-15"), () =>
        {
            var explorer = _panel.Find("Explorer.SelectionSync");
            try
            {
                _panel.Shot("before");
                _panel.Toggle(explorer, false, false);
                Evidence.Step("Turned Explorer.SelectionSync off.");
                _panel.Select("ModelContext.View.Filter");
                var filter = _panel.Find("Filter.SelectionSync");
                var filterState = filter.Patterns.Toggle.Pattern.ToggleState.Value;
                _steps.NewFilter([]);
                _panel.Toggle(_panel.Find("Filter.Categories"), true, false);
                _panel.Invoke("Filter.CategoryAll");
                var paused = _panel.Shown("Filter.Paused") is not null;
                _panel.Shot("paused");

                _panel.Toggle(filter, false, false);
                Evidence.Step("Turned Filter.SelectionSync off.");
                var off = _panel.Gone("Filter.Paused");
                _panel.Shot("off");
                _panel.Toggle(filter, true, false);
                Evidence.Step("Turned Filter.SelectionSync on.");
                var back = Retry.WhileFalse(() => _panel.Shown("Filter.Paused") is not null, Wait, ignoreException: true).Result;
                _panel.Select("ModelContext.View.Explorer");
                var explorerState = _panel.Find("Explorer.SelectionSync").Patterns.Toggle.Pattern.ToggleState.Value;
                _panel.Shot("after");
                Evidence.Values("Filter sync On after Explorer off; paused line; gone when Filter sync off; back when on; Explorer still Off",
                    $"Filter {filterState}; paused {paused}; gone {off}; back {back}; Explorer {explorerState}");

                Assert.AreEqual(ToggleState.On, filterState, "Turning Explorer sync off changed the Filter toggle.");
                Assert.IsTrue(paused, "No paused line above the limit.");
                Assert.IsTrue(off, "The paused line stayed with Filter sync off.");
                Assert.IsTrue(back, "The paused line did not come back with Filter sync on.");
                Assert.AreEqual(ToggleState.Off, explorerState, "The Filter toggle changed the Explorer toggle.");
            }
            finally
            {
                _panel.Select("ModelContext.View.Filter");
                _panel.Invoke("Filter.CategoryNone");
                _panel.Select("ModelContext.View.Explorer");
                _panel.Toggle(_panel.Find("Explorer.SelectionSync"), true, false);
            }
        });
    }

    [TestMethod]
    public void WhyHiddenNamesTempHide()
    {
        Record(CaseFiles.Flow("UI-16"), () =>
        {
            var current = ContextBarViewModel.CurrentModelLabel;
            var ducts = CaseFiles.Facts.CategoryNames["OST_DuctCurves"];
            _panel.Toggle(_steps.Document(current), true);
            WithBox(() =>
            {
                OpenBoxWithNone();
                _panel.Toggle(Named("Explorer.Category.Item", ducts), true, false);
                _panel.Toggle(_panel.Find("Explorer.Categories"), false, false);
                _panel.Settle();
                var model = _steps.DocumentName(current);
                _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
                var modelRow = _panel.Item(_panel.Find("Explorer.Rows"), cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(model)));
                _panel.Item(modelRow, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
                var row = RowUnder(model, ducts);
                _panel.Shot("before");

                _panel.Act(() => _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Eye")).Patterns.Invoke.Pattern.Invoke());
                Evidence.Step($"Hid {ducts} under the Current model with its eye.");
                var path = ExpandDown([model, ducts], 3);
                At(path).Patterns.SelectionItem.Pattern.Select();
                Evidence.Step($"Selected the instance row '{path[^1]}' (path {string.Join(" > ", path)}; the last child under {ducts} skips its subcategory rows).");
                var cause = Retry.WhileNull(() => _panel.Shown("WhyHiddenCause")?.Name is { Length: > 0 } text && text != WhyHiddenStripViewModel.CheckingText ? text : null,
                    Wait, ignoreException: true).Result;
                var fix = _panel.Shown("WhyHiddenFix") is not null;
                _panel.Shot("strip");

                _panel.Find("WhyHiddenClose").Patterns.Invoke.Pattern.Invoke();
                var closed = _panel.Gone("WhyHiddenCause");
                _panel.Shot("after");
                Evidence.Values($"'{HiddenReasons.Temporary.Text}' with Show; closed by ✕", $"'{cause}', Show {(fix ? "shown" : "missing")}; {(closed ? "closed" : "still open")}");

                Assert.AreEqual(HiddenReasons.Temporary.Text, cause);
                Assert.IsTrue(fix, "The strip has no Show for a hide the panel made.");
                Assert.IsTrue(closed, "✕ did not close the strip.");
            }, () =>
            {
                ResetLeftovers();
                _panel.Toggle(_steps.Document(current), false);
            });
        });
    }

    [TestMethod]
    public void KeepAsksForLinkHides()
    {
        Record(CaseFiles.Flow("UI-17"), () =>
        {
            var doors = CaseFiles.Facts.CategoryNames["OST_Doors"];
            WithBox(() =>
            {
                var link = _steps.DocumentName(CaseFiles.Facts.LinkLabel);
                _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
                var linkRow = _panel.Item(_panel.Find("Explorer.Rows"), cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(link)));
                _panel.Item(linkRow, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
                var row = RowUnder(link, doors);
                _panel.Shot("before");

                _panel.Act(() => _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Eye")).Patterns.Invoke.Pattern.Invoke());
                Evidence.Step($"Hid {doors} under the Architectural link (Hide in View).");
                _panel.Find("Temporary.Keep").Patterns.Invoke.Pattern.Invoke();
                Evidence.Step("Invoked Temporary.Keep.");
                var ask = Retry.WhileNull(() => _panel.Shown("Temporary.ConfirmText")?.Name is { Length: > 0 } text ? text : null, Wait, ignoreException: true).Result;
                _panel.Shot("ask");

                _panel.Find("Temporary.CancelKeep").Patterns.Invoke.Pattern.Invoke();
                Evidence.Step("Invoked Temporary.CancelKeep.");
                var cancelled = _panel.Gone("Temporary.ConfirmText");
                var bar = _panel.Shown("Temporary.Text")?.Name ?? string.Empty;
                _panel.Shot("after");

                _panel.Invoke("Temporary.Reset");
                var gone = _panel.Gone("Temporary.Text");
                _panel.Shot("reset");
                Evidence.Values("ask '… link elements stay hidden in this view'; Cancel keeps the bar; Reset clears it",
                    $"ask '{ask}'; {(cancelled ? "closed" : "still asking")}, bar '{bar}'; {(gone ? "no bar" : "bar still shown")}");

                StringAssert.Contains(ask ?? string.Empty, "stay hidden");
                Assert.IsTrue(cancelled, "Cancel left the Keep question.");
                Assert.IsTrue(bar.Any(char.IsDigit), $"Cancel dropped the temporary bar: '{bar}'.");
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }, ResetLeftovers);
        });
    }

    [TestMethod]
    public void RowMenuHidesLinkDoors()
    {
        Record(CaseFiles.Flow("UI-18"), () =>
        {
            var doors = CaseFiles.Facts.CategoryNames["OST_Doors"];
            WithBox(() =>
            {
                var link = _steps.DocumentName(CaseFiles.Facts.LinkLabel);
                _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
                var linkRow = _panel.Item(_panel.Find("Explorer.Rows"), cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(link)));
                _panel.Item(linkRow, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
                var row = RowUnder(link, doors);
                _panel.Shot("before");

                var syncing = _panel.Find("Explorer.SelectionSync").Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;
                var item = _panel.MenuItem(row, "Menu.HideShow");
                var items = _panel.MenuItems(item);
                Evidence.Step("Menu items: " + string.Join(", ", items) + ".");
                _panel.MenuShot(item, "menu");
                _panel.Act(() => item.Patterns.Invoke.Pattern.Invoke());
                Evidence.Step("Invoked Menu.HideShow (Invoke pattern).");
                var eye = _panel.Item(RowUnder(link, doors), cf => cf.ByAutomationId("Explorer.Row.Eye")).Properties.ItemStatus.ValueOrDefault;
                var bar = _panel.Shown("Temporary.Text")?.Name ?? string.Empty;
                _panel.Shot("after");

                _panel.Invoke("Temporary.Reset");
                var gone = _panel.Gone("Temporary.Text");
                _panel.Shot("reset");
                Evidence.Values("menu lists Zoom, Isolate, Hide, Halftone, Color, Why hidden (Select only while selection sync is off); eye Hidden, bar with a count; then no bar",
                    $"menu {string.Join(", ", items)}; eye {eye}, '{bar}'; {(gone ? "no bar" : "bar still shown")}");

                foreach (var expected in new[] { "Menu.Zoom", "Menu.Isolate", "Menu.HideShow", "Menu.Halftone", "Menu.Color", "Menu.WhyHidden" })
                {
                    CollectionAssert.Contains(items.ToList(), expected);
                }

                Assert.AreEqual(!syncing, items.Contains("Menu.SelectInHost"), $"Select must show only while Explorer selection sync is off (sync {(syncing ? "on" : "off")}).");

                Assert.AreEqual("Hidden", eye);
                Assert.IsTrue(bar.Any(char.IsDigit), $"The temporary bar shows no change count: '{bar}'.");
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }, ResetLeftovers);
        });
    }

    [TestMethod]
    public void BoxMenuIsolateHalftone()
    {
        Record(CaseFiles.Flow("UI-19"), () =>
        {
            var walls = CaseFiles.Facts.CategoryNames["OST_Walls"];
            WithBox(() =>
            {
                OpenBoxWithNone();
                _panel.Toggle(Named("Explorer.Category.Item", walls), true, false);
                _panel.Shot("before");

                var isolate = _panel.MenuItem(BoxRow(walls), "Explorer.CategoryMenu.Isolate");
                _panel.Act(() => isolate.Patterns.Invoke.Pattern.Invoke());
                var isolated = _panel.Shown("Temporary.Text")?.Name ?? string.Empty;
                _panel.Shot("isolate");
                _panel.Invoke("Temporary.Reset");
                var isolateGone = _panel.Gone("Temporary.Text");

                var halftone = _panel.MenuItem(BoxRow(walls), "Explorer.CategoryMenu.Halftone");
                _panel.Act(() => halftone.Patterns.Invoke.Pattern.Invoke());
                var halftoned = _panel.Shown("Temporary.Text")?.Name ?? string.Empty;
                _panel.Shot("halftone");
                _panel.Invoke("Temporary.Reset");
                var halftoneGone = _panel.Gone("Temporary.Text");
                _panel.Shot("after");
                Evidence.Values("Isolate: bar with a count, then no bar; Halftone: bar with a count, then no bar",
                    $"Isolate '{isolated}', {(isolateGone ? "no bar" : "bar still shown")}; Halftone '{halftoned}', {(halftoneGone ? "no bar" : "bar still shown")}");

                Assert.IsTrue(isolated.Any(char.IsDigit), $"Isolate showed no change count: '{isolated}'.");
                Assert.IsTrue(isolateGone, "Reset left the Isolate bar.");
                Assert.IsTrue(halftoned.Any(char.IsDigit), $"Halftone showed no change count: '{halftoned}'.");
                Assert.IsTrue(halftoneGone, "Reset left the Halftone bar.");
            }, ResetLeftovers);
        });
    }

    [TestMethod]
    public void ResultMenuHidesMatches()
    {
        Record(CaseFiles.Flow("UI-20"), () =>
        {
            var flow = CaseFiles.Flow("UI-20");
            var item = CaseFiles.Filter(flow.Cases[0]);
            _panel.Select("ModelContext.View.Filter");
            try
            {
                _steps.Build(item);
                var count = _panel.Count();
                _panel.Shot("before");

                var hide = _panel.MenuItem(_panel.Find("Filter.Mode"), "Menu.HideShow");
                _panel.Act(() => hide.Patterns.Invoke.Pattern.Invoke());
                Evidence.Step("Invoked Menu.HideShow from the result row's context menu.");
                var bar = _panel.Shown("Temporary.Text")?.Name ?? string.Empty;
                _panel.Shot("after");
                _panel.Invoke("Temporary.Reset");
                var gone = _panel.Gone("Temporary.Text");
                _panel.Shot("reset");
                Evidence.Values($"{item.ExpectedText}; bar with a count; then no bar",
                    $"{count}; '{bar}'; {(gone ? "no bar" : "bar still shown")}");

                Assert.AreEqual(item.ExpectedCount, count);
                Assert.IsTrue(bar.Any(char.IsDigit), $"The temporary bar shows no change count: '{bar}'.");
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }
            finally
            {
                ResetLeftovers();
                _panel.Select("ModelContext.View.Explorer");
            }
        });
    }

    [TestMethod]
    public void SavedDotFollowsKeep()
    {
        Record(CaseFiles.Flow("UI-21"), () =>
        {
            var doors = CaseFiles.Facts.CategoryNames["OST_Doors"];
            var warning = ThemeColors.Warning;
            _panel.Select("ModelContext.Scope.ActiveView");
            try
            {
                WithBox(() =>
                {
                    var link = _steps.DocumentName(CaseFiles.Facts.LinkLabel);
                    _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
                    var linkRow = _panel.Item(_panel.Find("Explorer.Rows"), cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(link)));
                    _panel.Item(linkRow, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
                    var before = DotPixels(link, doors, warning);
                    _panel.Shot("before");

                    _panel.Act(() => _panel.Item(RowUnder(link, doors), cf => cf.ByAutomationId("Explorer.Row.Eye")).Patterns.Invoke.Pattern.Invoke());
                    Evidence.Step($"Hid {doors} under the Architectural link (Hide in View) in Active view.");
                    _panel.Find("Temporary.Keep").Patterns.Invoke.Pattern.Invoke();
                    _panel.Invoke("Temporary.ConfirmKeep");
                    Evidence.Step("Invoked Temporary.Keep and Temporary.ConfirmKeep: the linked doors stay hidden, saved with the view.");
                    var kept = DotPixels(link, doors, warning);
                    var keptEye = EyeOf(link, doors);
                    _panel.Shot("kept");

                    _panel.Act(() => _panel.Item(RowUnder(link, doors), cf => cf.ByAutomationId("Explorer.Row.Eye")).Patterns.Invoke.Pattern.Invoke());
                    Evidence.Step($"Turned the {doors} eye back on (Show).");
                    var after = DotPixels(link, doors, warning);
                    var shownEye = EyeOf(link, doors);
                    _panel.Shot("after");
                    Evidence.Values($"dot pixels (hue of {ColorTranslator.ToHtml(warning)}, any row opacity): under {DotMinimum} before; {DotMinimum} or more after Keep, eye Hidden; under {DotMinimum} after Show, eye Shown",
                        $"{before} before; {kept} after Keep, eye {keptEye}; {after} after Show, eye {shownEye}");

                    Assert.IsLessThan(DotMinimum, before, "The saved-hide dot shows before any hide.");
                    Assert.IsGreaterThanOrEqualTo(DotMinimum, kept, "No saved-hide dot after Keep.");
                    Assert.AreEqual("Hidden", keptEye);
                    Assert.IsLessThan(DotMinimum, after, "The saved-hide dot stayed after Show.");
                    Assert.AreEqual("Shown", shownEye);
                }, ResetLeftovers);
            }
            finally
            {
                _panel.Select("ModelContext.Scope.WholeModel");
            }
        });
    }

    private static string? EyeOf(string link, string name)
    {
        return _panel.Item(RowUnder(link, name), cf => cf.ByAutomationId("Explorer.Row.Eye")).Properties.ItemStatus.ValueOrDefault;
    }

    private static int DotPixels(string link, string name, Color dot)
    {
        _panel.Settle();
        var row = RowUnder(link, name);
        var left = _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Text")).BoundingRectangle.Right;
        foreach (var id in new[] { "Explorer.Row.Count", "Explorer.Row.HiddenCount" })
        {
            if (row.FindFirstDescendant(cf => cf.ByAutomationId(id)) is { IsOffscreen: false } part)
            {
                left = Math.Max(left, part.BoundingRectangle.Right);
            }
        }

        var eye = _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Eye")).BoundingRectangle;
        var rowBounds = row.BoundingRectangle;
        var area = new Rectangle(left, rowBounds.Top, Math.Max(1, eye.Left - left), rowBounds.Height);
        using var image = WindowShot.Capture(_panel.PanelWindow, area);
        return WindowShot.CountHue(image, dot, 10);
    }

    private static AutomationElement BoxRow(string name)
    {
        var row = Retry.WhileNull(() => _panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Explorer.Category.Row"))
                .FirstOrDefault(candidate => candidate.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Category.Item"))?.Name == name),
            Wait, ignoreException: true).Result;
        Assert.IsNotNull(row, $"No box row '{name}'.");
        return row!;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    private static void WithSite(Action body)
    {
        var site = _panel.Find("ModelContext.Documents").FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
            .FirstOrDefault(item => item.Name.Contains("Site", StringComparison.OrdinalIgnoreCase));
        if (site is null)
        {
            Assert.Inconclusive($"Load the '{CaseFiles.Facts.SiteLinkTitle}' link in the sample.");
        }

        _panel.Toggle(site!, true);
        Evidence.Step($"Checked '{site!.Name}' in ModelContext.Documents.");
        WithBox(body, () => _panel.Toggle(_steps.Document("Site"), false));
    }

    private static void WithBox(Action body, Action? after = null)
    {
        try
        {
            body();
        }
        finally
        {
            _panel.Toggle(_panel.Find("Explorer.Categories"), true, false);
            _panel.SetValue(_panel.Find("Explorer.CategorySearch"), string.Empty, false);
            CheckAllBoxes();
            _panel.Toggle(_panel.Find("Explorer.Categories"), false, false);
            after?.Invoke();
        }
    }

    private static void CheckAllBoxes()
    {
        _panel.Find("Explorer.CategoryAll").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(Retry.WhileFalse(() => _panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Explorer.Category.Item"))
                .All(box => box.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On), Wait, ignoreException: true).Result,
            "All left categories unchecked.");
        _panel.Settle();
    }

    private static void OpenBoxWithNone()
    {
        _panel.Toggle(_panel.Find("Explorer.Categories"), true, false);
        _panel.SetValue(_panel.Find("Explorer.CategorySearch"), string.Empty, false);
        _panel.Invoke("Explorer.CategoryNone");
        Assert.IsTrue(Retry.WhileFalse(() => Checked().Count == 0, Wait, ignoreException: true).Result, "None left checks.");
        Evidence.Step("Opened Explorer.Categories and clicked None.");
    }

    private static void ResetLeftovers()
    {
        if (_panel.Shown("Temporary.Reset") is not null)
        {
            _panel.Invoke("Temporary.Reset");
            Evidence.Step("Reset the temporary changes the case left.");
        }
    }

    private static AutomationElement Named(string automationId, string name)
    {
        var found = Retry.WhileNull(() => _panel.Root.FindAllDescendants(cf => cf.ByAutomationId(automationId)).FirstOrDefault(element => element.Name == name),
            Wait, ignoreException: true).Result;
        Assert.IsNotNull(found, $"{automationId} '{name}' was not found.");
        return found!;
    }

    private static ToggleState Box(string name)
    {
        return Named("Explorer.Category.Item", name).Patterns.Toggle.Pattern.ToggleState.Value;
    }

    private static bool WaitBox(string name, ToggleState state)
    {
        return Retry.WhileFalse(() => Box(name) == state, Wait, ignoreException: true).Result;
    }

    private static HashSet<string> Checked()
    {
        return _panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Explorer.Category.Item"))
            .Where(box => box.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On)
            .Select(box => box.Name).ToHashSet();
    }

    private static List<string> RowNames()
    {
        return _panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Explorer.Category.Row")).Select(row => row.Name).ToList();
    }

    private static AutomationElement[] Rows()
    {
        return _panel.Find("Explorer.Rows").FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
    }

    private static AutomationElement RowUnder(string parent, string name)
    {
        var row = Retry.WhileNull(() =>
        {
            var items = Rows();
            var start = Array.FindIndex(items, item => item.Name == parent);
            return start < 0 ? null : items.Skip(start + 1).FirstOrDefault(item => item.Name == name);
        }, Wait, ignoreException: true).Result;
        Assert.IsNotNull(row, $"No row '{name}' under '{parent}'.");
        if (row!.IsOffscreen)
        {
            row.Patterns.ScrollItem.Pattern.ScrollIntoView();
        }

        return row;
    }

    private static AutomationElement At(IReadOnlyList<string> path)
    {
        var items = Rows();
        var index = -1;
        foreach (var name in path)
        {
            index = Array.FindIndex(items, index + 1, item => item.Name == name);
            Assert.IsGreaterThanOrEqualTo(0, index, $"No row '{string.Join(" > ", path)}'.");
        }

        if (items[index].IsOffscreen)
        {
            items[index].Patterns.ScrollItem.Pattern.ScrollIntoView();
        }

        return items[index];
    }

    private static string[] ExpandDown(string[] path, int levels)
    {
        for (var level = 0; level < levels; level++)
        {
            var row = At(path);
            var before = Rows();
            var index = Array.FindIndex(before, item => item.Equals(row));
            _panel.Settle();
            var expand = Retry.WhileNull(() => row.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Expand")), Wait, ignoreException: true).Result;
            Assert.IsNotNull(expand, $"'{string.Join(" > ", path)}' has no expander; rows: {string.Join(" | ", before.Select(item => item.Name))}.");
            Assert.IsTrue(Retry.WhileFalse(() => expand!.IsEnabled, Wait, ignoreException: true).Result,
                $"Expand on '{path[^1]}' stayed disabled.");
            expand!.Patterns.Invoke.Pattern.Invoke();
            var child = Retry.WhileNull(() => Rows() is var items && items.Length > before.Length
                    ? items[index + (level == 0 ? items.Length - before.Length : 1)]
                    : null,
                Wait, ignoreException: true).Result;
            Assert.IsNotNull(child, $"Expanding '{path[^1]}' showed no child row.");
            path = [.. path, child!.Name];
        }

        return path;
    }

    private static int RowCount(AutomationElement row)
    {
        var text = _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Count")).Name;
        var digits = new string(text.TakeWhile(ch => !char.IsWhiteSpace(ch)).Where(char.IsDigit).ToArray());
        return int.Parse(digits, CultureInfo.InvariantCulture);
    }

    private static bool Inside(Rectangle inner, Rectangle outer)
    {
        return inner.Width > 0 && inner.Left >= outer.Left - 1 && inner.Right <= outer.Right + 1
               && inner.Top >= outer.Top - 1 && inner.Bottom <= outer.Bottom + 1;
    }
}
