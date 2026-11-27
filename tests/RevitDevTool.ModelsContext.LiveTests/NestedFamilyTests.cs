using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Visibility;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class NestedFamilyTests : RecordedTest
{
    private const long TableId = 738550;

    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly VisibilityAction[] Actions =
        [VisibilityAction.Isolate, VisibilityAction.Hide, VisibilityAction.Halftone, VisibilityAction.Color];

    [Test]
    [Explicit("Run alone with a copy of 'Snowdon Towers Sample Architectural.rvt' as the active model and '{3D}' active (MCP open_document, then user-action-activate-view.cs with ViewName '{3D}'): it never opens a document or switches views.")]
    public void NestedChairsFollowHost()
    {
        Record(new CaseItem
        {
            Id = "NE-01",
            Description = "Current model table with nested chairs: Isolate, Hide, Halftone and Color reach the chairs; Reset restores them",
            Expected = JsonSerializer.SerializeToElement("chairs follow the host"),
            ManualSteps = ["Snowdon Architectural copy, {3D} active: Isolate, Hide, Halftone and Color on 'Table-Dining Round w Chairs' (738550) also act on its chairs; Reset shows them with their own graphics."]
        }, () =>
        {
            var model = ActiveModel.Host;
            var view = SampleViews.Active.ActiveView;
            if (view is not View3D)
            {
                Assert.Inconclusive($"The active view '{view.Name}' is not a 3D view; activate '{{3D}}' with user-action-activate-view.cs.");
            }

            var host = HostOf(model, view);
            var nested = host.GetSubComponentIds().ToList();
            Evidence.Step($"Host '{host.Name}' ({host.Id}) in '{view.Name}' with {nested.Count} nested instances.");
            var actions = new RevitViewActions();
            var outcomes = new List<string>();
            var problems = new List<string>();
            foreach (var action in Actions)
            {
                Act(model, view, host, nested, actions, action, outcomes, problems);
            }

            Evidence.Values("each nested instance follows the host; Reset restores them", string.Join("; ", outcomes));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }, writesView: true);
    }

    private static void Act(Document model, View view, FamilyInstance host, List<ElementId> nested, RevitViewActions actions,
        VisibilityAction action, List<string> outcomes, List<string> problems)
    {
        Adapter.Run(actions.ResetAsync(null, null, None));
        var before = nested.ToDictionary(id => id, view.GetElementOverrides);
        var plan = new VisibilityPlan(action, action == VisibilityAction.Color ? Red : null,
            [new ElementsStep(DocumentRegistry.KeyOf(model), [host.Id.ToLong()])], null);
        try
        {
            Evidence.Time(action.ToString(), () => Adapter.Run(actions.ExecuteAsync(plan, None)));
            var missed = nested.Where(id => !Follows(view, id, action)).Select(id => id.ToLong()).ToList();
            outcomes.Add($"{action}: {nested.Count - missed.Count}/{nested.Count} follow");
            Evidence.Step($"{action} on the host through RevitViewActions.ExecuteAsync: {nested.Count - missed.Count} of {nested.Count} nested follow.");
            if (missed.Count > 0)
            {
                problems.Add($"{action}: nested {string.Join(", ", missed)} did not follow the host");
            }
        }
        finally
        {
            Adapter.Run(actions.ResetAsync(null, null, None));
            Evidence.Step($"{action}: Reset through RevitViewActions.ResetAsync.");
        }

        var hidden = nested.Where(id => !IsVisible(model, view, id)).Select(id => id.ToLong()).ToList();
        if (hidden.Count > 0)
        {
            problems.Add($"{action}: Reset left nested {string.Join(", ", hidden)} hidden");
        }

        var changed = before.Where(pair => !VisibilityRun.Same(view.GetElementOverrides(pair.Key), pair.Value)).Select(pair => pair.Key.ToLong()).ToList();
        if (changed.Count > 0)
        {
            problems.Add($"{action}: Reset did not restore the overrides of nested {string.Join(", ", changed)}");
        }
    }

    private static bool Follows(View view, ElementId id, VisibilityAction action)
    {
        return action switch
        {
            VisibilityAction.Isolate => view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, id),
            VisibilityAction.Hide => !view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, id),
            VisibilityAction.Halftone => view.GetElementOverrides(id).Halftone,
            VisibilityAction.Color => IsRed(view.GetElementOverrides(id).ProjectionLineColor),
            _ => false
        };
    }

    private static bool IsRed(Color color)
    {
        return color.IsValid && color.Red == Red.R && color.Green == Red.G && color.Blue == Red.B;
    }

    private static bool IsVisible(Document model, View view, ElementId id)
    {
        return model.GetElement(id) is { } element
               && !element.IsHidden(view)
               && (!view.IsTemporaryHideIsolateActive() || view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, id));
    }

    private static FamilyInstance HostOf(Document model, View view)
    {
        if (model.GetElement(TableId.ToElementId()) is FamilyInstance table && table.GetSubComponentIds().Count > 0)
        {
            return table;
        }

        Evidence.Step($"Element {TableId} is not a family instance with nested instances in this model; using the first one in '{view.Name}'.");
        var found = new FilteredElementCollector(model, view.Id).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .FirstOrDefault(instance => instance.GetSubComponentIds().Any(id => model.GetElement(id) is FamilyInstance));
        if (found is null)
        {
            Assert.Inconclusive($"'{view.Name}' shows no family instance with nested family instances; open a copy of 'Snowdon Towers Sample Architectural.rvt'.");
        }

        return found!;
    }
}
