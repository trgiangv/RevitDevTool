using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using RevitDevTool.ModelsContext.LiveTests;
using System.Diagnostics;
using System.Globalization;

namespace RevitDevTool.ModelsContext.UiTests;

internal sealed class RevitPanel : IDisposable
{
    private static readonly TimeSpan FindLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SettleLimit = TimeSpan.FromSeconds(90);

    private readonly UIA3Automation _automation;

    private RevitPanel(UIA3Automation automation, AutomationElement root, IntPtr window)
    {
        _automation = automation;
        Root = root;
        Window = window;
    }

    public AutomationElement Root { get; }

    public IntPtr Window { get; }

    public static RevitPanel Attach(string hostTitle)
    {
        var l2 = Evidence.HostOf("L2");
        var process = Process.GetProcessesByName("Revit")
            .FirstOrDefault(candidate => (l2 is null || candidate.Id == l2)
                && candidate.MainWindowTitle.Contains(hostTitle, StringComparison.OrdinalIgnoreCase));
        if (process is null)
        {
            Assert.Inconclusive(l2 is null
                ? $"Open '{hostTitle}.rvt' in Revit."
                : $"Open '{hostTitle}.rvt' in the Revit that ran L2 of this run (PID {l2}).");
        }

        Evidence.HostProcessId = process!.Id;
        var automation = new UIA3Automation();
        var application = FlaUI.Core.Application.Attach(process!);
        var root = FindPanel(application, automation);
        if (root is null)
        {
            var button = application.GetMainWindow(automation, FindLimit)?
                .FindFirstDescendant(cf => cf.ByName(DevTools.ModelsContext.ViewModels.ModelContextViewModel.PanelTitle).And(cf.ByControlType(ControlType.Button)));
            button?.Patterns.Invoke.PatternOrDefault?.Invoke();
            root = Retry.WhileNull(() => FindPanel(application, automation), FindLimit, ignoreException: true).Result;
        }

        if (root is null)
        {
            automation.Dispose();
            Assert.Inconclusive("Show the Model Context panel in Revit (RevitDevTool tab, Model Context).");
        }

        return new RevitPanel(automation, root!, process!.MainWindowHandle);
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
        action();
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
        return Root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    public AutomationElement Last(string automationId)
    {
        var elements = Retry.WhileEmpty(() => Root.FindAllDescendants(cf => cf.ByAutomationId(automationId)), FindLimit,
            ignoreException: true).Result;
        Assert.IsTrue(elements is { Length: > 0 }, automationId + " was not found in the panel.");
        return elements![^1];
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

    public AutomationElement Item(AutomationElement container, Func<ConditionFactory, ConditionBase> condition)
    {
        var item = Retry.WhileNull(() => container.FindFirstDescendant(condition), FindLimit, ignoreException: true).Result;
        Assert.IsNotNull(item, $"No matching item in {container.AutomationId}.");
        return item!;
    }

    public int Count()
    {
        var digits = new string(Find("Filter.ResultCount").Name.Where(char.IsDigit).ToArray());
        return int.Parse(digits, CultureInfo.InvariantCulture);
    }

    public void Shot(string label)
    {
        Evidence.Image(label, path => Capture.Element(Root).ToFile(path));
        Evidence.Image(label + "-revit", path => WindowShot.Save(Window, path));
    }

    public void Dispose()
    {
        _automation.Dispose();
    }

    private static AutomationElement? FindPanel(FlaUI.Core.Application application, UIA3Automation automation)
    {
        foreach (var window in application.GetAllTopLevelWindows(automation))
        {
            if (window.FindFirstDescendant(cf => cf.ByAutomationId("ModelContext.Panel")) is { } panel)
            {
                return panel;
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
