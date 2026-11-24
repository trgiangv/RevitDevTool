using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Exceptions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using RevitDevTool.ModelsContext.LiveTests;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RevitDevTool.ModelsContext.UiTests;

internal sealed class RevitPanel : IDisposable
{
    public const string RevitPidVariable = "MODELS_CONTEXT_REVIT_PID";
    public const string HostTitleVariable = "MODELS_CONTEXT_HOST_TITLE";

    private static readonly TimeSpan FindLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SettleLimit = TimeSpan.FromSeconds(90);

    private readonly UIA3Automation _automation;
    private int _processId;

    private RevitPanel(UIA3Automation automation, AutomationElement root, IntPtr window, IntPtr panelWindow)
    {
        _automation = automation;
        Root = root;
        Window = window;
        PanelWindow = panelWindow;
    }

    public AutomationElement Root { get; }

    public IntPtr Window { get; }

    public IntPtr PanelWindow { get; }

    public static RevitPanel Attach(string defaultTitle)
    {
        var hostTitle = Environment.GetEnvironmentVariable(HostTitleVariable) is { Length: > 0 } given ? given : defaultTitle;
        var host = (int.TryParse(Environment.GetEnvironmentVariable(RevitPidVariable), out var pid) ? pid : (int?)null)
            ?? Evidence.HostOf("L2");
        var candidates = Process.GetProcessesByName("Revit")
            .Where(candidate => (host is null || candidate.Id == host)
                && candidate.MainWindowTitle.Contains(hostTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            Assert.Inconclusive(host is null
                ? $"Open '{hostTitle}.rvt' in Revit."
                : $"Open '{hostTitle}.rvt' in the Revit with PID {host}.");
        }

        if (candidates.Count > 1)
        {
            Assert.Inconclusive($"Several Revit processes show '{hostTitle}' (PIDs {string.Join(", ", candidates.Select(candidate => candidate.Id))}). Set {RevitPidVariable} or run L2 first.");
        }

        var process = candidates[0];
        Evidence.HostProcessId = process.Id;
        var automation = new UIA3Automation();
        var application = FlaUI.Core.Application.Attach(process);
        var found = Retry.WhileNull(() => FindPanel(application, automation), FindLimit, ignoreException: true).Result;
        if (found is not var (root, panelWindow))
        {
            automation.Dispose();
            Assert.Inconclusive("The Model Context panel is not shown. L2 ViewSetupTests.PreparesSample shows it through the Revit API (scripts/test-models-context.ps1 runs it first).");
            throw new InvalidOperationException();
        }

        return new RevitPanel(automation, root, process.MainWindowHandle, panelWindow) { _processId = process.Id };
    }

    public PanelActivity Activity => PanelActivity.Parse(Root.Properties.ItemStatus.ValueOrDefault);

    public int Settle()
    {
        var idle = Retry.WhileFalse(() => Activity.IsIdle, SettleLimit, ignoreException: true).Result;
        Assert.IsTrue(idle, $"The panel did not settle: {Root.Properties.ItemStatus.ValueOrDefault}");
        return Activity.Generation;
    }

    public void Act(Action action)
    {
        var before = Settle();
        var front = ForegroundProcess();
        action();
        NoteFront(front);
        var done = Retry.WhileFalse(() => Activity is { IsIdle: true } activity && activity.Generation > before, SettleLimit,
            ignoreException: true).Result;
        Assert.IsTrue(done, $"The panel did not finish the action: {Root.Properties.ItemStatus.ValueOrDefault}");
    }

    public AutomationElement Find(string automationId)
    {
        var element = TryFind(automationId);
        Assert.IsNotNull(element, automationId + " was not found in the panel.");
        return element!;
    }

    public AutomationElement? TryFind(string automationId, TimeSpan? wait = null)
    {
        return Retry.WhileNull(() => Root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), wait ?? FindLimit,
            ignoreException: true).Result;
    }

    public AutomationElement? Shown(string automationId)
    {
        return Root.FindAllDescendants(cf => cf.ByAutomationId(automationId)).FirstOrDefault(element => !element.IsOffscreen);
    }

    public bool Gone(string automationId)
    {
        return Retry.WhileFalse(() => Shown(automationId) is null, FindLimit, ignoreException: true).Result;
    }

    public AutomationElement Last(string automationId)
    {
        var elements = Retry.WhileEmpty(() => Root.FindAllDescendants(cf => cf.ByAutomationId(automationId)), FindLimit,
            ignoreException: true).Result;
        Assert.IsTrue(elements is { Length: > 0 }, automationId + " was not found in the panel.");
        return elements!.LastOrDefault(element => !element.IsOffscreen) ?? elements![^1];
    }

    public void Invoke(string automationId)
    {
        Act(() => Find(automationId).Patterns.Invoke.Pattern.Invoke());
    }

    public void Select(string automationId)
    {
        var element = Find(automationId);
        if (!element.Patterns.SelectionItem.Pattern.IsSelected.Value)
        {
            Act(() => element.Patterns.SelectionItem.Pattern.Select());
        }
    }

    public void Toggle(AutomationElement element, bool on, bool runs = true)
    {
        var wanted = on ? ToggleState.On : ToggleState.Off;
        if (element.Patterns.Toggle.Pattern.ToggleState.Value == wanted)
        {
            return;
        }

        if (runs)
        {
            Act(() => element.Patterns.Toggle.Pattern.Toggle());
            return;
        }

        element.Patterns.Toggle.Pattern.Toggle();
        Assert.IsTrue(Retry.WhileFalse(() => element.Patterns.Toggle.Pattern.ToggleState.Value == wanted, FindLimit).Result,
            element.AutomationId + " did not toggle.");
    }

    public void SetValue(AutomationElement element, string value, bool runs = true)
    {
        if (runs)
        {
            Act(() => element.Patterns.Value.Pattern.SetValue(value));
            return;
        }

        element.Patterns.Value.Pattern.SetValue(value);
    }

    public void Choose(AutomationElement combo, string name, bool runs = true)
    {
        var item = Choice(combo, name);
        if (item.Patterns.SelectionItem.Pattern.IsSelected.Value)
        {
            return;
        }

        if (runs)
        {
            Act(() => SelectChoice(combo, name, item));
            return;
        }

        SelectChoice(combo, name, item);
    }

    public AutomationElement Item(AutomationElement container, Func<ConditionFactory, ConditionBase> condition)
    {
        var item = Retry.WhileNull(() => container.FindFirstDescendant(condition), FindLimit, ignoreException: true).Result;
        Assert.IsNotNull(item, $"No matching item in {container.AutomationId}.");
        return item!;
    }

    public AutomationElement Popup(Func<ConditionFactory, ConditionBase> condition)
    {
        var popup = TryPopup(condition, FindLimit);
        Assert.IsNotNull(popup, "No popup window of the panel holds the item.");
        return popup!;
    }

    public AutomationElement? TryPopup(Func<ConditionFactory, ConditionBase> condition, TimeSpan wait)
    {
        return Retry.WhileNull(() => PopupWindows().FirstOrDefault(window => window.FindFirstDescendant(condition) is not null),
            wait, ignoreException: true).Result;
    }

    private IEnumerable<AutomationElement> PopupWindows()
    {
        foreach (var top in _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(_processId)))
        {
            if (top.Properties.NativeWindowHandle.ValueOrDefault is var handle && handle != Window && handle != PanelWindow)
            {
                yield return top;
            }

            foreach (var owned in top.FindAllChildren(cf => cf.ByClassName("Popup")))
            {
                yield return owned;
            }
        }
    }

    public string Message()
    {
        return Shown("ModelContext.Message")?.Name is { } text && !string.IsNullOrWhiteSpace(text) ? text : string.Empty;
    }

    public string NewMessage(string before)
    {
        var now = Message();
        if (now.Length > 0 && now == before)
        {
            Evidence.Step($"The panel already showed '{before}' before this case; only a new message counts.");
            return string.Empty;
        }

        return now;
    }

    public int Count()
    {
        var digits = new string(Find("Filter.ResultCount").Name.Where(char.IsDigit).ToArray());
        return int.Parse(digits, CultureInfo.InvariantCulture);
    }

    public void Shot(string label)
    {
        Evidence.Image(label, path => WindowShot.Save(PanelWindow, path, Root.BoundingRectangle));
        Evidence.Image(label + "-revit", path => WindowShot.Save(Window, path));
    }

    public void Dispose()
    {
        _automation.Dispose();
    }

    private AutomationElement Choice(AutomationElement combo, string name)
    {
        var container = combo.Patterns.ItemContainer.Pattern;
        var item = Retry.WhileNull(() => container.FindItemByProperty(null!, _automation.PropertyLibrary.Element.Name, name),
            FindLimit, ignoreException: true).Result;
        Assert.IsNotNull(item, $"No item '{name}' in {combo.AutomationId}.");
        return item!;
    }

    private void SelectChoice(AutomationElement combo, string name, AutomationElement item)
    {
        try
        {
            item.Patterns.SelectionItem.Pattern.Select();
            return;
        }
        catch (ElementNotAvailableException)
        {
        }

        var drop = combo.Patterns.ExpandCollapse.Pattern;
        try
        {
            drop.Expand();
            Evidence.Step($"Opened {combo.AutomationId} for one Select: WPF had not made its items yet.");
            Choice(combo, name).Patterns.SelectionItem.Pattern.Select();
        }
        finally
        {
            if (drop.ExpandCollapseState.Value != ExpandCollapseState.Collapsed)
            {
                drop.Collapse();
            }
        }
    }

    private void NoteFront(int front)
    {
        if (front != _processId && ForegroundProcess() == _processId)
        {
            Evidence.Step($"Revit came to the front during this action (the front window was in process {front}).");
        }
    }

    private static int ForegroundProcess()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var process);
        return (int)process;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    private static (AutomationElement Panel, IntPtr Window)? FindPanel(FlaUI.Core.Application application, UIA3Automation automation)
    {
        foreach (var window in application.GetAllTopLevelWindows(automation))
        {
            if (window.FindFirstDescendant(cf => cf.ByAutomationId("ModelContext.Panel")) is { } panel)
            {
                return (panel, window.Properties.NativeWindowHandle.Value);
            }
        }

        return null;
    }
}

internal readonly record struct PanelActivity(bool IsIdle, int Generation)
{
    public static PanelActivity Parse(string? status)
    {
        var parts = (status ?? string.Empty).Split(' ');
        return parts.Length == 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var generation)
            ? new PanelActivity(parts[0] == "Idle", generation)
            : new PanelActivity(false, -1);
    }
}
