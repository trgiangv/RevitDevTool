using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using RevitDevTool.ModelsContext.LiveTests;
using System.Runtime.InteropServices;

namespace RevitDevTool.ModelsContext.UiTests;

[TestClass]
[DoNotParallelize]
public sealed class PanelFlowTests : RecordedTest
{
    private static RevitPanel _panel = null!;

    [ClassInitialize]
    public static void Open(TestContext context)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        Evidence.Layer = "L3";
        _panel = RevitPanel.Attach(CaseFiles.Facts.HostTitle);
        _panel.Settle();
        _panel.Select("ModelContext.Scope.WholeModel");
        _panel.Select("ModelContext.View.Filter");
        _panel.Toggle(Document(CaseFiles.Facts.LinkLabel), true);
        _panel.Toggle(Document(ContextBarViewModel.CurrentModelLabel), false);
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
            Build(off);
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

            Build(item);
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
            Build(item);
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
            NewFilter([]);
            Switch(name);
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
    public void LivePausesAboveLimit()
    {
        var flow = CaseFiles.Flow("UI-04");
        Record(flow, () =>
        {
            var item = CaseFiles.Filter(flow.Cases[0]);
            NewFilter([]);
            _panel.Shot("before");

            _panel.Toggle(_panel.Find("Filter.Categories"), true, false);
            _panel.Invoke("Filter.CategoryAll");
            Evidence.Step("Opened Categories and clicked All.");
            var count = _panel.Count();
            var live = _panel.Find("Filter.Live").Patterns.Toggle.Pattern.ToggleState.Value;
            var paused = _panel.Shown("Filter.Paused")?.Name;
            _panel.Shot("paused");

            _panel.Invoke("Filter.CategoryNone");
            Evidence.Step("Clicked None.");
            var cleared = _panel.Shown("Filter.Paused") is null;
            _panel.Shot("after");
            Evidence.Values($"{item.ExpectedText}; Live On; '{FilterRunner.LivePausedText}'; then no line",
                $"{count}; Live {live}; '{paused}'; then {(cleared ? "no line" : "line still shown")}");

            Assert.AreEqual(item.ExpectedCount, count);
            Assert.IsGreaterThan(FilterRunner.LivePauseLimit, count, "SC-02 must exceed the Live limit for this flow.");
            Assert.AreEqual(ToggleState.On, live, "The Live switch must keep the user's choice.");
            Assert.AreEqual(FilterRunner.LivePausedText, paused);
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
                    cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(DocumentName(CaseFiles.Facts.LinkLabel))));
                var eye = _panel.Item(row, cf => cf.ByAutomationId("Explorer.Row.Eye"));
                _panel.Act(() => eye.Patterns.Invoke.Pattern.Invoke());
                Evidence.Step("Invoked the eye on the Architectural link's model row.");
                var bar = _panel.Find("Temporary.Text").Name;
                _panel.Shot("after");

                _panel.Invoke("Temporary.Reset");
                Evidence.Step("Invoked Temporary.Reset.");
                var gone = _panel.TryFind("Temporary.Text", TimeSpan.FromSeconds(1)) is null;
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
                var hidden = Eye(LinkCategoryRow(category)).Properties.ItemStatus.Value;
                var bar = _panel.Find("Temporary.Text").Name;
                _panel.Shot("after");

                _panel.Invoke("Temporary.Reset");
                Evidence.Step("Invoked Temporary.Reset.");
                var shown = Eye(LinkCategoryRow(category)).Properties.ItemStatus.Value;
                var gone = _panel.Shown("Temporary.Text") is null;
                _panel.Shot("reset");
                Evidence.Values($"eye {nameof(RowEye.Hidden)}, temporary bar with 1 change; then eye {nameof(RowEye.Shown)}, no bar",
                    $"eye {hidden}, '{bar}'; then eye {shown}, {(gone ? "no bar" : "bar still shown")}");

                Assert.AreEqual(nameof(RowEye.Hidden), hidden);
                StringAssert.Contains(bar, "1");
                Assert.AreEqual(nameof(RowEye.Shown), shown);
                Assert.IsTrue(gone, "Reset left the temporary bar.");
            }
            finally
            {
                _panel.Select("ModelContext.View.Filter");
            }
        });
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    private static void Build(FilterCaseData item)
    {
        Evidence.Step($"Built {item.Id} through UI Automation: {item.Description}.");
        NewFilter(item.Filter.Categories ?? []);
        foreach (var rule in item.Filter.Rules ?? [])
        {
            Assert.IsFalse(rule.IsGroup, $"{item.Id}: the UI flow builds flat rules only.");
            AddRule(rule);
        }

        if (item.Filter.Match == nameof(SetMatch.Any))
        {
            _panel.Act(() => _panel.Last("Filter.Set.Connector").Patterns.Invoke.Pattern.Invoke());
        }
    }

    private static AutomationElement ExpandLink(string category)
    {
        var rows = _panel.Find("Explorer.Rows");
        var link = DocumentName(CaseFiles.Facts.LinkLabel);
        _panel.Find("Explorer.CollapseAll").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(Retry.WhileFalse(() => RowAfter(rows, link, category) is null, TimeSpan.FromSeconds(10), ignoreException: true).Result,
            "Collapse all left link rows open.");
        var linkRow = _panel.Item(rows, cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(link)));
        _panel.Item(linkRow, cf => cf.ByAutomationId("Explorer.Row.Expand")).Patterns.Invoke.Pattern.Invoke();
        var row = Retry.WhileNull(() => RowAfter(rows, link, category), TimeSpan.FromSeconds(10), ignoreException: true).Result;
        Assert.IsNotNull(row, $"No {category} row under the Architectural link.");
        return row!;
    }

    private static AutomationElement LinkCategoryRow(string category)
    {
        var row = RowAfter(_panel.Find("Explorer.Rows"), DocumentName(CaseFiles.Facts.LinkLabel), category);
        Assert.IsNotNull(row, $"The {category} row under the Architectural link is gone.");
        return row!;
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

    private static AutomationElement Document(string part)
    {
        return _panel.Item(_panel.Find("ModelContext.Documents"),
            cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(DocumentName(part))));
    }

    private static string DocumentName(string part)
    {
        var items = _panel.Find("ModelContext.Documents").FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        var match = items.FirstOrDefault(item => item.Name.Contains(part, StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(match, $"No document named like '{part}'.");
        return match.Name;
    }

    private static void NewFilter(IReadOnlyList<string> categories)
    {
        var before = _panel.Settle();
        _panel.Find("Filter.Add").Patterns.Invoke.Pattern.Invoke();
        AcceptDiscard(before);
        if (categories.Count == 0)
        {
            return;
        }

        _panel.Toggle(_panel.Find("Filter.Categories"), true, false);
        foreach (var category in categories)
        {
            var name = CaseFiles.Facts.CategoryNames[category];
            _panel.SetValue(_panel.Find("Filter.CategorySearch"), name, false);
            var item = _panel.Item(_panel.Find("Filter.CategoryList"),
                cf => cf.ByAutomationId("Filter.Category.Item").And(cf.ByName(name)));
            _panel.Toggle(item, true);
        }

        _panel.SetValue(_panel.Find("Filter.CategorySearch"), string.Empty, false);
    }

    private static void Switch(string name)
    {
        var before = _panel.Settle();
        var source = _panel.Find("Filter.Source");
        source.Patterns.ExpandCollapse.Pattern.Expand();
        var item = _panel.Item(source, cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(name)));
        item.Patterns.SelectionItem.Pattern.Select();
        if (source.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded)
        {
            source.Patterns.ExpandCollapse.Pattern.Collapse();
        }

        AcceptDiscard(before);
    }

    private static void AcceptDiscard(int before)
    {
        AutomationElement? discard = null;
        var settled = Retry.WhileFalse(() =>
        {
            discard = _panel.Root.FindFirstDescendant(cf => cf.ByAutomationId("Filter.DiscardSwitch"));
            return discard is not null || _panel.Activity is { IsIdle: true } activity && activity.Generation > before;
        }, TimeSpan.FromSeconds(60), ignoreException: true).Result;
        Assert.IsTrue(settled, "The filter switch did not finish.");
        if (discard is not null)
        {
            _panel.Act(() => discard.Patterns.Invoke.Pattern.Invoke());
        }
    }

    private static void AddRule(NodeData rule)
    {
        var rules = _panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Filter.Rule.Parameter")).Length;
        _panel.Last("Filter.Rule.Add").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(Retry.WhileFalse(() => _panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Filter.Rule.Parameter")).Length > rules,
            TimeSpan.FromSeconds(10)).Result, "+ Condition added no rule.");

        _panel.SetValue(_panel.Last("Filter.Rule.Parameter"), rule.Parameter!);
        var op = (FilterOperator)Enum.Parse(typeof(FilterOperator), rule.Operator!);
        ChooseOperator(EnumText.Of(op));
        if (FilterOperators.NeedsValue(op))
        {
            _panel.SetValue(_panel.Last("Filter.Rule.Value"), rule.Value);
        }

        if (rule.CaseSensitive)
        {
            _panel.Toggle(_panel.Last("Filter.Rule.CaseSensitive"), true);
        }
    }

    private static void ChooseOperator(string op)
    {
        var combo = _panel.Last("Filter.Rule.Operator");
        combo.Patterns.ExpandCollapse.Pattern.Expand();
        var item = _panel.Item(combo, cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(op)));
        if (item.Patterns.SelectionItem.Pattern.IsSelected.Value)
        {
            combo.Patterns.ExpandCollapse.Pattern.Collapse();
            return;
        }

        _panel.Act(() => item.Patterns.SelectionItem.Pattern.Select());
        if (combo.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded)
        {
            combo.Patterns.ExpandCollapse.Pattern.Collapse();
        }
    }
}
