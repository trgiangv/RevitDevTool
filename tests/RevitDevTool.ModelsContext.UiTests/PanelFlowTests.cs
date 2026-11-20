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
        _panel.Toggle(_steps.Document(CaseFiles.Facts.LinkLabel), true);
        _panel.Toggle(_steps.Document(ContextBarViewModel.CurrentModelLabel), false);
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
    public void LivePausesAboveLimit()
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
