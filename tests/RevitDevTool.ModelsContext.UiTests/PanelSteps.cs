using DevTools.ModelsContext.Model;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using RevitDevTool.ModelsContext.LiveTests;

namespace RevitDevTool.ModelsContext.UiTests;

internal sealed class PanelSteps(RevitPanel panel)
{
    public void Build(FilterCaseData item)
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
            for (var attempt = 0; attempt < 3 && panel.Last("Filter.Set.Connector").Name != "OR"; attempt++)
            {
                panel.Act(() => panel.Last("Filter.Set.Connector").Patterns.Invoke.Pattern.Invoke());
                Retry.WhileFalse(() => panel.Last("Filter.Set.Connector").Name == "OR", TimeSpan.FromSeconds(3),
                    ignoreException: true);
            }

            Assert.AreEqual("OR", panel.Last("Filter.Set.Connector").Name, "The connector stayed AND.");
        }
    }

    public AutomationElement Document(string part)
    {
        return panel.Item(panel.Find("ModelContext.Documents"),
            cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(DocumentName(part))));
    }

    public string DocumentName(string part)
    {
        var items = panel.Find("ModelContext.Documents").FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        var match = items.FirstOrDefault(item => item.Name.Contains(part, StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(match, $"No document named like '{part}'.");
        return match.Name;
    }

    public void NewFilter(IReadOnlyList<string> categories)
    {
        var before = panel.Settle();
        panel.Find("Filter.Add").Patterns.Invoke.Pattern.Invoke();
        AcceptDiscard(before);
        if (categories.Count == 0)
        {
            return;
        }

        panel.Toggle(panel.Find("Filter.Categories"), true, false);
        foreach (var category in categories)
        {
            var name = CaseFiles.Facts.CategoryNames[category];
            panel.SetValue(panel.Find("Filter.CategorySearch"), name, false);
            var item = panel.Item(panel.Find("Filter.CategoryList"),
                cf => cf.ByAutomationId("Filter.Category.Item").And(cf.ByName(name)));
            panel.Toggle(item, true);
        }

        panel.SetValue(panel.Find("Filter.CategorySearch"), string.Empty, false);
    }

    public void Switch(string name)
    {
        var before = panel.Settle();
        panel.Choose(panel.Find("Filter.Source"), name, false);
        AcceptDiscard(before);
    }

    public void AddRule(NodeData rule)
    {
        var rules = panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Filter.Rule.Parameter")).Length;
        panel.Last("Filter.Rule.Add").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(Retry.WhileFalse(() => panel.Root.FindAllDescendants(cf => cf.ByAutomationId("Filter.Rule.Parameter")).Length > rules,
            TimeSpan.FromSeconds(10)).Result, "+ Condition added no rule.");

        panel.SetValue(panel.Last("Filter.Rule.Parameter"), rule.Parameter!);
        var op = (FilterOperator)Enum.Parse(typeof(FilterOperator), rule.Operator!);
        panel.Choose(panel.Last("Filter.Rule.Operator"), EnumText.Of(op));
        if (FilterOperators.NeedsValue(op))
        {
            panel.SetValue(panel.Last("Filter.Rule.Value"), rule.Value);
        }

        if (rule.CaseSensitive)
        {
            panel.Toggle(panel.Last("Filter.Rule.CaseSensitive"), true);
        }
    }

    private void AcceptDiscard(int before)
    {
        AutomationElement? discard = null;
        var settled = Retry.WhileFalse(() =>
        {
            discard = panel.Shown("Filter.DiscardSwitch");
            return discard is not null || panel.Activity is { IsIdle: true } activity && activity.Generation > before;
        }, TimeSpan.FromSeconds(60), ignoreException: true).Result;
        Assert.IsTrue(settled, "The filter switch did not finish.");
        if (discard is not null)
        {
            panel.Act(() => discard.Patterns.Invoke.Pattern.Invoke());
        }
    }
}
