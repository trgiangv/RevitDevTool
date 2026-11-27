using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using RevitDevTool.ModelsContext.LiveTests;
using System.Runtime.InteropServices;

namespace RevitDevTool.ModelsContext.UiTests;

[TestClass]
[DoNotParallelize]
public sealed class PanelFlowTests : RecordedTest
{
    private const string SwatchHex = "#F44336";

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
        _panel.Select("ModelContext.View.Filter");
        _steps.CheckOnly(CaseFiles.Facts.LinkLabel);
    }

    [ClassCleanup]
    public static void Close()
    {
        _panel?.Dispose();
    }

    [TestMethod]
    public void CaseToggleChangesCount()
    {
        var flow = CaseFiles.Flow("UI-01");
        Record(flow, () =>
        {
            var off = CaseFiles.Filter(flow.Cases[0]);
            var on = CaseFiles.Filter(flow.Cases[1]);
            _panel.Shot("before");
            _steps.Build(off);
            var first = _panel.Count();
            _panel.Shot("after-off");

            _panel.Toggle(_panel.Last("Filter.Rule.CaseSensitive"), on.Filter.Rules![0].CaseSensitive);
            Evidence.Step("Turned Aa on in the rule (Toggle pattern on Filter.Rule.CaseSensitive).");
            var second = _panel.Count();
            _panel.Shot("after");

            Evidence.Values($"{off.ExpectedText}, then {on.ExpectedText}", $"{first}, then {second}");
            Assert.AreEqual(off.ExpectedCount, first);
            Assert.AreEqual(on.ExpectedCount, second);
        });
    }

    [TestMethod]
    public void ConnectorMakesOrGroup()
    {
        var flow = CaseFiles.Flow("UI-02");
        Record(flow, () =>
        {
            var item = CaseFiles.Filter(flow.Cases[0]);
            _panel.Shot("before");

            _steps.Build(item);
            _panel.Shot("after");

            var count = _panel.Count();
            Evidence.Values(item.ExpectedText, count);
            Assert.AreEqual(item.ExpectedCount, count);
        });
    }

    [TestMethod]
    public void SavedFilterReloads()
    {
        var flow = CaseFiles.Flow("UI-03");
        Record(flow, () =>
        {
            var item = CaseFiles.Filter(flow.Cases[0]);
            var name = "uia-live " + Guid.NewGuid().ToString("N").Substring(0, 8);
            _panel.Shot("before");
            _steps.Build(item);
            var parameter = _panel.Last("Filter.Rule.Parameter").Patterns.Value.Pattern.Value.Value;
            var built = _panel.Count();
            Assert.AreEqual(item.Filter.Rules![0].Parameter, parameter);
            Assert.AreEqual(item.ExpectedCount, built);
            _panel.Find("Filter.Rename").Patterns.Invoke.Pattern.Invoke();
            _panel.SetValue(_panel.Find("Filter.Name"), name, false);
            _panel.Find("Filter.Save").Patterns.Invoke.Pattern.Invoke();
            Assert.IsTrue(Retry.WhileTrue(() => _panel.Find("Filter.Save").IsEnabled, TimeSpan.FromSeconds(10)).Result, "Save stayed enabled.");
            Evidence.Step($"Renamed the filter to '{name}' and clicked Save.");
            _panel.Shot("saved");
            _steps.NewFilter([]);
            _steps.Switch(name);
            Evidence.Step($"Made a new filter, then picked '{name}' in the source list.");

            try
            {
                var count = _panel.Count();
                _panel.Shot("after");
                Evidence.Values(item.ExpectedText, count);
                Assert.AreEqual(item.ExpectedCount, count);
            }
            finally
            {
                _panel.Invoke("Filter.Delete");
                Evidence.Step($"Deleted '{name}'.");
            }
        });
    }

    [TestMethod]
    public void SyncPausesAboveLimit()
    {
        var flow = CaseFiles.Flow("UI-04");
        Record(flow, () =>
        {
            var item = CaseFiles.Filter(flow.Cases[0]);
            _steps.NewFilter([]);
            _panel.Shot("before");

            _panel.Toggle(_panel.Find("Filter.Categories"), true, false);
            _panel.Invoke("Filter.CategoryAll");
            Evidence.Step("Opened Categories and clicked All.");
            var count = _panel.Count();
            var sync = _panel.Find("Filter.SelectionSync").Patterns.Toggle.Pattern.ToggleState.Value;
            var paused = _panel.Shown("Filter.Paused")?.Name;
            _panel.Shot("paused");

            _panel.Invoke("Filter.CategoryNone");
            Evidence.Step("Clicked None.");
            var cleared = _panel.Shown("Filter.Paused") is null;
            _panel.Shot("after");
            Evidence.Values($"{item.ExpectedText}; Selection sync On; '{FilterRunner.SelectionSyncPausedText}'; then no line",
                $"{count}; Selection sync {sync}; '{paused}'; then {(cleared ? "no line" : "line still shown")}");

            Assert.AreEqual(item.ExpectedCount, count);
            Assert.IsGreaterThan(FilterRunner.SelectionSyncPauseLimit, count, "SC-02 must exceed the selection sync limit for this flow.");
            Assert.AreEqual(ToggleState.On, sync, "Selection sync must keep the user's choice.");
            Assert.AreEqual(FilterRunner.SelectionSyncPausedText, paused);
            Assert.IsTrue(cleared, "The paused line stayed at 0 matches.");
        });
    }
    [TestMethod]
    public void LinkHideShowsTemporaryBar()
    {
        var flow = CaseFiles.Flow("UI-05");
        Record(flow, () =>
        {
            _panel.Select("ModelContext.View.Explorer");
            try
            {
                _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
                Evidence.Step("Collapsed all rows so the link model row is on screen.");
                _panel.Shot("before");
                var row = _panel.Item(_panel.Find("Explorer.Rows"),
                    cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(_steps.DocumentName(CaseFiles.Facts.LinkLabel))));
                var eye = _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Eye"));
                _panel.Act(() => eye.Patterns.Invoke.Pattern.Invoke());
                Evidence.Step("Invoked the eye on the Architectural link's model row.");
                var bar = _panel.Find("Temporary.Text").Name;
                _panel.Shot("after");

                _panel.Invoke("Temporary.Reset");
                Evidence.Step("Invoked Temporary.Reset.");
                var gone = _panel.Gone("Temporary.Text");
                _panel.Shot("reset");
                Evidence.Values("temporary bar with 1 change, then no bar", $"'{bar}', then {(gone ? "no bar" : "bar still shown")}");

                StringAssert.Contains(bar, "1");
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }
            finally
            {
                _panel.Select("ModelContext.View.Filter");
            }
        });
    }

    [TestMethod]
    public void LinkDoorsHideThenReset()
    {
        var flow = CaseFiles.Flow("UI-06");
        Record(flow, () =>
        {
            _panel.Select("ModelContext.View.Explorer");
            try
            {
                var category = CaseFiles.Facts.CategoryNames[flow.Category!];
                var row = ExpandLink(category);
                Evidence.Step("Collapsed all rows and expanded the Architectural link row.");
                _panel.Shot("before");

                _panel.Act(() => Eye(row).Patterns.Invoke.Pattern.Invoke());
                Evidence.Step($"Invoked the eye on {category} under the Architectural link (T3, Hide in View).");
                var hidden = LinkEye(category).Properties.ItemStatus.Value;
                var bar = _panel.Find("Temporary.Text").Name;
                _panel.Shot("after");

                _panel.Invoke("Temporary.Reset");
                Evidence.Step("Invoked Temporary.Reset.");
                var gone = _panel.Gone("Temporary.Text");
                var shown = LinkEye(category).Properties.ItemStatus.Value;
                _panel.Shot("reset");
                Evidence.Values($"eye {nameof(HideState.Hidden)}, temporary bar with 1 change; then eye {nameof(HideState.Shown)}, no bar",
                    $"eye {hidden}, '{bar}'; then eye {shown}, {(gone ? "no bar" : "bar still shown")}");

                Assert.AreEqual(nameof(HideState.Hidden), hidden);
                StringAssert.Contains(bar, "1");
                Assert.AreEqual(nameof(HideState.Shown), shown);
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }
            finally
            {
                _panel.Select("ModelContext.View.Filter");
            }
        });
    }

    [TestMethod]
    public void LinkTypeIsolateThenReset()
    {
        var flow = CaseFiles.Flow("UI-07");
        Record(flow, () =>
        {
            _panel.Select("ModelContext.View.Explorer");
            try
            {
                var category = CaseFiles.Facts.CategoryNames[flow.Category!];
                ExpandLink(category);
                var link = _steps.DocumentName(CaseFiles.Facts.LinkLabel);
                var others = LinkCategories(link).Where(name => name != category).ToList();
                var type = ExpandChild(ExpandChild([link, category]));
                Evidence.Step($"Collapsed all rows, expanded the Architectural link, {category} and its first family: type row '{type[^1]}' under '{type[^2]}'.");
                _panel.Shot("before");

                var stale = _panel.Message();
                var tool = RowTool(type, "Explorer.Row.Isolate");
                Evidence.Time("isolate", () => _panel.Act(() => tool.Patterns.Invoke.Pattern.Invoke()));
                Evidence.Step("Invoked Isolate on the selected type row (Explorer.Row.Isolate runs the row menu's Isolate item).");
                var bar = _panel.TryFind("Temporary.Text")?.Name ?? string.Empty;
                var message = _panel.NewMessage(stale);
                var typeEye = EyeAt(type);
                var eyes = LinkEyes(link, others);
                var hidden = others.Where(name => eyes.GetValueOrDefault(name) == nameof(HideState.Hidden)).ToList();
                Evidence.Step("Eyes after Isolate: " + string.Join(", ", others.Select(name => $"{name} {eyes.GetValueOrDefault(name) ?? "off screen"}")) + ".");
                _panel.Shot("after");

                Evidence.Time("reset", () => _panel.Invoke("Temporary.Reset"));
                Evidence.Step("Invoked Temporary.Reset.");
                var gone = _panel.Gone("Temporary.Text");
                var after = LinkEyes(link, hidden);
                var back = hidden.Where(name => after.GetValueOrDefault(name) != nameof(HideState.Shown)).ToList();
                _panel.Shot("reset");
                Evidence.Values($"bar with a count, no error, type eye {nameof(HideState.Shown)}, another category {nameof(HideState.Hidden)}; then all {nameof(HideState.Shown)}, no bar",
                    $"'{bar}', error '{message}', type eye {typeEye}, {hidden.Count} of {others.Count} categories {nameof(HideState.Hidden)}; then {(back.Count == 0 ? "all shown" : "still hidden: " + string.Join(", ", back))}, {(gone ? "no bar" : "bar still shown")}");

                Assert.IsTrue(bar.Any(char.IsDigit), $"The temporary bar shows no change count: '{bar}'.");
                Assert.AreEqual(string.Empty, message);
                Assert.AreEqual(nameof(HideState.Shown), typeEye);
                Assert.IsNotEmpty(hidden, "No other link category reads Hidden after Isolate.");
                Assert.IsEmpty(back, "Reset left link categories hidden: " + string.Join(", ", back));
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }
            finally
            {
                ResetLeftovers();
                _panel.Select("ModelContext.View.Filter");
            }
        });
    }

    [TestMethod]
    public void LinkTypeColorThenReset()
    {
        var flow = CaseFiles.Flow("UI-08");
        Record(flow, () =>
        {
            _panel.Select("ModelContext.View.Explorer");
            try
            {
                var category = CaseFiles.Facts.CategoryNames[flow.Category!];
                ExpandLink(category);
                var link = _steps.DocumentName(CaseFiles.Facts.LinkLabel);
                var type = ExpandChild(ExpandChild([link, category]));
                Evidence.Step($"Collapsed all rows, expanded the Architectural link, {category} and its first family: type row '{type[^1]}' under '{type[^2]}'.");
                _panel.Shot("before");

                var stale = _panel.Message();
                var tool = RowTool(type, "Explorer.Row.Color");
                _panel.Act(() => tool.Patterns.Invoke.Pattern.Invoke());
                var picker = _panel.Popup(ConfirmButton);
                FirstSwatch(picker).Patterns.Invoke.Pattern.Invoke();
                var confirm = _panel.Item(picker, ConfirmButton);
                Evidence.Time("color", () => _panel.Act(() => confirm.Patterns.Invoke.Pattern.Invoke()));
                Evidence.Step("Invoked Color on the selected type row (Explorer.Row.Color runs the row menu's Color item), the first picker swatch, then Confirm.");
                var bar = _panel.Shown("Temporary.Text")?.Name ?? string.Empty;
                var message = _panel.NewMessage(stale);
                Retry.WhileFalse(() => ColorAt(type) == SwatchHex, TimeSpan.FromSeconds(10), ignoreException: true);
                var mark = ColorAt(type);
                _panel.Shot("after");

                Evidence.Time("reset", () => _panel.Invoke("Temporary.Reset"));
                Evidence.Step("Invoked Temporary.Reset.");
                var gone = _panel.Gone("Temporary.Text");
                Retry.WhileFalse(() => string.IsNullOrEmpty(ColorAt(type)), TimeSpan.FromSeconds(10), ignoreException: true);
                var cleared = ColorAt(type);
                _panel.Shot("reset");
                Evidence.Values($"bar with a count, no error, row color {SwatchHex}; then no color, no bar",
                    $"'{bar}', error '{message}', row color '{mark}'; then color '{cleared}', {(gone ? "no bar" : "bar still shown")}");

                Assert.IsTrue(bar.Any(char.IsDigit), $"The temporary bar shows no change count: '{bar}'.");
                Assert.AreEqual(string.Empty, message);
                Assert.AreEqual(SwatchHex, mark);
                Assert.IsTrue(string.IsNullOrEmpty(cleared), $"Reset left the row color '{cleared}'.");
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }
            finally
            {
                ClosePicker();
                ResetLeftovers();
                _panel.Select("ModelContext.View.Filter");
            }
        });
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    private static void ResetLeftovers()
    {
        if (_panel.Shown("Temporary.Reset") is not null)
        {
            _panel.Invoke("Temporary.Reset");
            Evidence.Step("Reset the temporary changes the case left.");
        }
    }

    private static List<string> LinkCategories(string link)
    {
        var documents = _panel.Find("ModelContext.Documents").FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
            .Select(item => item.Name).ToHashSet();
        var items = _panel.Find("Explorer.Rows").FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var start = Array.FindIndex(items, item => item.Name == link);
        return items.Skip(start + 1).TakeWhile(item => !documents.Contains(item.Name)).Select(item => item.Name).ToList();
    }

    private static AutomationElement? AtPath(IReadOnlyList<string> path, int offset = 0)
    {
        var items = _panel.Find("Explorer.Rows").FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var index = -1;
        foreach (var name in path)
        {
            index = Array.FindIndex(items, index + 1, item => item.Name == name);
            if (index < 0)
            {
                return null;
            }
        }

        index += offset;
        return index < items.Length ? items[index] : null;
    }

    private static AutomationElement OnScreen(string[] path)
    {
        var row = AtPath(path);
        Assert.IsNotNull(row, $"No row '{string.Join(" > ", path)}'.");
        if (row!.IsOffscreen)
        {
            row.Patterns.ScrollItem.Pattern.ScrollIntoView();
            Evidence.Step($"Scrolled '{path[^1]}' into view (ScrollItem pattern).");
        }

        return row;
    }

    private static string[] ExpandChild(string[] path)
    {
        var row = OnScreen(path);
        var next = AtPath(path, 1)?.Name;
        _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
        var child = Retry.WhileNull(() => AtPath(path, 1) is { } item && item.Name != next ? item : null,
            TimeSpan.FromSeconds(10), ignoreException: true).Result;
        Assert.IsNotNull(child, $"Expanding '{path[^1]}' showed no child row.");
        return [.. path, child!.Name];
    }

    private static AutomationElement RowTool(string[] path, string automationId)
    {
        OnScreen(path).Patterns.SelectionItem.Pattern.Select();
        var tool = Retry.WhileNull(() => AtPath(path)?.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            TimeSpan.FromSeconds(10), ignoreException: true).Result;
        Assert.IsNotNull(tool, $"No {automationId} on the selected row '{path[^1]}'.");
        Assert.IsTrue(Retry.WhileFalse(() => tool!.IsEnabled, TimeSpan.FromSeconds(5), ignoreException: true).Result,
            $"{automationId} is disabled: {tool!.Properties.HelpText.ValueOrDefault}");
        Evidence.Step($"Selected '{path[^1]}' (SelectionItem pattern).");
        return tool!;
    }

    private static string? EyeAt(IReadOnlyList<string> path)
    {
        return AtPath(path)?.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye"))?.Properties.ItemStatus.ValueOrDefault;
    }

    private static Dictionary<string, string?> LinkEyes(string link, IReadOnlyCollection<string> names)
    {
        var items = _panel.Find("Explorer.Rows").FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var start = Array.FindIndex(items, item => item.Name == link);
        var wanted = names.ToHashSet();
        var eyes = new Dictionary<string, string?>();
        foreach (var item in items.Skip(start + 1).Where(item => wanted.Contains(item.Name) && !eyes.ContainsKey(item.Name)))
        {
            eyes[item.Name] = EyeOf(item);
        }

        if (wanted.Any(name => eyes.GetValueOrDefault(name) is null))
        {
            ScrollFor(link, wanted, eyes);
        }

        return eyes;
    }

    private static void ScrollFor(string link, HashSet<string> wanted, Dictionary<string, string?> eyes)
    {
        var rows = _panel.Find("Explorer.Rows");
        var scroll = rows.Patterns.Scroll.Pattern;
        Evidence.Step("Scrolled the rows from the top down to read the eyes of rows the list had not realized (Scroll pattern).");
        scroll.SetScrollPercent(-1, 0);
        Retry.WhileFalse(() => scroll.VerticalScrollPercent.Value <= 0, TimeSpan.FromSeconds(5), ignoreException: true);
        Collect(rows, link, wanted, eyes);
        while (wanted.Any(name => eyes.GetValueOrDefault(name) is null) && scroll.VerticalScrollPercent.Value < 100)
        {
            var before = scroll.VerticalScrollPercent.Value;
            scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
            Retry.WhileFalse(() => scroll.VerticalScrollPercent.Value > before, TimeSpan.FromSeconds(5), ignoreException: true);
            Collect(rows, link, wanted, eyes);
            if (scroll.VerticalScrollPercent.Value <= before)
            {
                return;
            }
        }
    }

    private static void Collect(AutomationElement rows, string link, HashSet<string> wanted, Dictionary<string, string?> eyes)
    {
        var items = rows.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var start = Array.FindIndex(items, item => item.Name == link);
        foreach (var item in items.Skip(start + 1).Where(item => wanted.Contains(item.Name) && eyes.GetValueOrDefault(item.Name) is null))
        {
            eyes[item.Name] = EyeOf(item);
        }
    }

    private static string? EyeOf(AutomationElement row)
    {
        return row.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye"))?.Properties.ItemStatus.ValueOrDefault;
    }

    private static string? ColorAt(IReadOnlyList<string> path)
    {
        return AtPath(path)?.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Color"))?.Properties.ItemStatus.ValueOrDefault;
    }

    private static ConditionBase ConfirmButton(ConditionFactory cf)
    {
        return cf.ByControlType(ControlType.Button).And(cf.ByName("Confirm"));
    }

    private static ConditionBase CancelButton(ConditionFactory cf)
    {
        return cf.ByControlType(ControlType.Button).And(cf.ByName("Cancel"));
    }

    private static void ClosePicker()
    {
        if (_panel.TryPopup(ConfirmButton, TimeSpan.FromSeconds(1)) is not { } picker)
        {
            return;
        }

        _panel.Item(picker, CancelButton).Patterns.Invoke.Pattern.Invoke();
        var closed = Retry.WhileFalse(() => _panel.TryPopup(ConfirmButton, TimeSpan.Zero) is null, TimeSpan.FromSeconds(10), ignoreException: true).Result;
        Evidence.Step(closed ? "Closed the color picker the case left open (Cancel)." : "The color picker stayed open after Cancel.");
    }

    private static AutomationElement FirstSwatch(AutomationElement picker)
    {
        var buttons = picker.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .Where(button => button.ClassName == "Button" && string.IsNullOrEmpty(button.Name) && button.Patterns.Invoke.IsSupported)
            .ToList();
        Assert.IsNotEmpty(buttons, "The color picker shows no swatches.");
        var smallest = buttons.Min(button => button.BoundingRectangle.Height);
        return buttons.First(button => button.BoundingRectangle.Height <= smallest + 2);
    }

    private static AutomationElement ExpandLink(string category)
    {
        var rows = _panel.Find("Explorer.Rows");
        var link = _steps.DocumentName(CaseFiles.Facts.LinkLabel);
        _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(Retry.WhileFalse(() => RowAfter(rows, link, category) is null, TimeSpan.FromSeconds(10), ignoreException: true).Result,
            "Collapse all left link rows open.");
        var linkRow = _panel.Item(rows, cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(link)));
        _panel.Item(linkRow, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
        var row = Retry.WhileNull(() => RowAfter(rows, link, category), TimeSpan.FromSeconds(10), ignoreException: true).Result;
        Assert.IsNotNull(row, $"No {category} row under the Architectural link.");
        return row!;
    }

    private static AutomationElement LinkEye(string category)
    {
        var link = _steps.DocumentName(CaseFiles.Facts.LinkLabel);
        var eye = Retry.WhileNull(() => RowAfter(_panel.Find("Explorer.Rows"), link, category)
                ?.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye")),
            TimeSpan.FromSeconds(10), ignoreException: true).Result;
        Assert.IsNotNull(eye, $"No eye on the {category} row under the Architectural link.");
        return eye!;
    }

    private static AutomationElement? RowAfter(AutomationElement rows, string parent, string name)
    {
        var items = rows.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var start = Array.FindIndex(items, item => item.Name == parent);
        return start < 0 ? null : items.Skip(start + 1).FirstOrDefault(item => item.Name == name && !item.IsOffscreen);
    }

    private static AutomationElement Eye(AutomationElement row)
    {
        return _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Eye"));
    }
}
