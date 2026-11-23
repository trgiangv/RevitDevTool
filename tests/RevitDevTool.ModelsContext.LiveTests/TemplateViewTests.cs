using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Visibility;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed record TemplateTarget(string Name, VisibilityStep Step, IReadOnlyList<ElementId> Elements, ElementId? Category);

[TestFixture]
public sealed class TemplateViewTests : RecordedTest
{
    private const string Ok = "ok";
    private const int ProbeSize = 25;

    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly VisibilityAction[] Actions = [VisibilityAction.Hide, VisibilityAction.Isolate, VisibilityAction.Halftone, VisibilityAction.Color];

    private static readonly Lazy<CaseFile<UserActionSetup, UserActionCase>> LazyData =
        new(() => CaseFiles.Read<CaseFile<UserActionSetup, UserActionCase>>("user-actions.json"));

    [Test]
    [Explicit("Run alone after the MCP steps user-action-template-view.cs and user-action-activate-view.cs (ViewName set to the view it made): it checks the view that is active now and never switches views.")]
    public void LockedTemplateActionsStaySafe()
    {
        Record(Areas.Find(LazyData.Value.Cases, "US-17"), () =>
        {
            var host = ActiveModel.Host;
            var view = SampleModel.Application.ActiveUIDocument.ActiveView;
            if (!LocksGraphics(host, view))
            {
                Assert.Inconclusive($"The active view '{view.Name}' has no template that controls V/G Overrides Model and Annotation. Run the MCP step scripts/models-context/user-action-template-view.cs, then user-action-activate-view.cs with ViewName set to the view it reports.");
            }

            var template = view.ViewTemplateId;
            Evidence.Step($"Active view '{view.Name}' uses the template '{host.GetElement(template).Name}', which controls V/G Overrides Model and Annotation.");
            var outcomes = new List<string>();
            var problems = new List<string>();
            foreach (var target in Targets(host, view))
            {
                foreach (var action in Actions)
                {
                    Act(host, view, target, action, template, outcomes, problems);
                }
            }

            Keep(host, view, template, outcomes, problems);
            Evidence.Values("each action ok or refused; Reset leaves the view as before", string.Join("; ", outcomes));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        });
    }

    [Test]
    [Explicit("Run alone with a plan active whose template controls V/G Filters (see US-18): it checks the view that is active now and never switches views.")]
    public void LockedTemplateLinkColorDraws()
    {
        Record(Areas.Find(LazyData.Value.Cases, "US-18"), () =>
        {
            var host = ActiveModel.Host;
            var view = SampleModel.Application.ActiveUIDocument.ActiveView;
            if (!LocksFilters(host, view))
            {
                Assert.Inconclusive($"The active view '{view.Name}' has no template that controls V/G Overrides Filters. Activate a plan such as L3 with its template.");
            }

            var template = view.ViewTemplateId;
            var step = VisibilityRun.LinkTypeStep("OST_Doors", 1);
            var plan = new VisibilityPlan(VisibilityAction.Color, Red, [step], null);
            var actions = new RevitViewActions();
            var problems = new List<string>();
            var group = new TransactionGroup(host, "Model Context template link color");
            group.Start();
            try
            {
                Evidence.Time("color", () => Adapter.Run(actions.ExecuteAsync(plan, None)));
                var properties = view.TemporaryViewModes?.IsModeActive(TemporaryViewMode.TemporaryViewProperties) == true;
                var drawn = VisibilityRun.LinkFilterDrawn(view, new RgbData { R = Red.R, G = Red.G, B = Red.B }, false);
                Evidence.Step($"Color on a linked door type: temporary view properties {properties}, panel filter on the view with red {drawn}.");
                Shot(host, view, "after");
                if (!properties || !drawn)
                {
                    problems.Add($"expected the panel filter inside Temporary View Properties, got properties {properties}, drawn {drawn}");
                }

                Evidence.Time("reset", () => Adapter.Run(actions.ResetAsync(None)));
                var left = VisibilityRun.PanelFilterCount(host);
                Evidence.Step($"Reset: {left} panel filters left in the model.");
                if (left > 0)
                {
                    problems.Add($"Reset left {left} Model Context filters");
                }

                Check(view, "link color", new Dictionary<ElementId, OverrideGraphicSettings>(), null, null, template, problems);
            }
            finally
            {
                if (group.HasStarted())
                {
                    group.RollBack();
                }

                group.Dispose();
            }

            Evidence.Values("filter drawn inside Temporary View Properties; Reset leaves no panel filter", string.Join("; ", problems));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }, writesView: true);
    }

    private static void Shot(Document host, View view, string label)
    {
        if (Evidence.Images)
        {
            Evidence.Image(label, path => ViewImage.Export(host, view, path));
        }
    }

    private static bool LocksFilters(Document host, View view)
    {
        if (view.ViewTemplateId == ElementId.InvalidElementId || host.GetElement(view.ViewTemplateId) is not View template)
        {
            return false;
        }

        var controlled = new HashSet<long>(template.GetTemplateParameterIds().Select(id => id.ToLong()));
        controlled.ExceptWith(template.GetNonControlledTemplateParameterIds().Select(id => id.ToLong()));
        return controlled.Contains((long)BuiltInParameter.VIS_GRAPHICS_FILTERS);
    }

    private static void Act(Document host, View view, TemplateTarget target, VisibilityAction action, ElementId template,
        List<string> outcomes, List<string> problems)
    {
        var name = $"{target.Name} {action}";
        var elements = target.Elements.ToDictionary(id => id, view.GetElementOverrides);
        var category = target.Category is { } id ? view.GetCategoryOverrides(id) : null;
        var modified = host.IsModified;
        var actions = new RevitViewActions();
        var plan = new VisibilityPlan(action, action == VisibilityAction.Color ? Red : null, [target.Step], null);
        var group = new TransactionGroup(host, "Model Context template check");
        group.Start();
        try
        {
            var outcome = Outcome(() => Adapter.Run(actions.ExecuteAsync(plan, None)));
            outcomes.Add($"{name}: {outcome}");
            Evidence.Step($"{name}: {outcome}.");
            Note(problems, name, outcome);
            var reset = Outcome(() => Adapter.Run(actions.ResetAsync(None)));
            if (reset != Ok)
            {
                problems.Add($"{name}: Reset gave {reset}");
            }

            Check(view, name, elements, target.Category, category, template, problems);
        }
        finally
        {
            if (group.HasStarted())
            {
                group.RollBack();
            }

            group.Dispose();
        }

        if (host.IsModified != modified)
        {
            problems.Add($"{name}: the model was {(modified ? "unmodified" : "modified")} after the rolled-back group");
        }
    }

    private static void Keep(Document host, View view, ElementId template, List<string> outcomes, List<string> problems)
    {
        var target = Targets(host, view).FirstOrDefault();
        if (target is null)
        {
            return;
        }

        var modified = host.IsModified;
        var actions = new RevitViewActions();
        var plan = new VisibilityPlan(VisibilityAction.Hide, null, [target.Step], null);
        var group = new TransactionGroup(host, "Model Context template keep");
        group.Start();
        try
        {
            var hidden = Outcome(() => Adapter.Run(actions.ExecuteAsync(plan, None)));
            var kept = hidden == Ok ? Outcome(() => Adapter.Run(actions.KeepAsync(None))) : "not run";
            outcomes.Add($"{target.Name} Hide then Keep: {hidden}, {kept}");
            Evidence.Step($"{target.Name} Hide then Keep inside a transaction group: {hidden}, {kept}.");
            Note(problems, target.Name + " Hide", hidden);
            Note(problems, target.Name + " Keep", kept);
            if (kept == Ok && view.IsTemporaryHideIsolateActive())
            {
                problems.Add("Keep left the temporary mode on");
            }
        }
        finally
        {
            if (group.HasStarted())
            {
                group.RollBack();
            }

            group.Dispose();
        }

        Evidence.Step("Rolled the group back.");
        if (target.Elements.Any(id => host.GetElement(id) is { } element && element.IsHidden(view)))
        {
            problems.Add("Keep: elements stayed hidden after the group was rolled back");
        }

        if (view.ViewTemplateId != template)
        {
            problems.Add("Keep: the view template changed");
        }

        if (host.IsModified != modified)
        {
            problems.Add("Keep: the model's modified state changed after the rolled-back group");
        }
    }

    private static void Check(View view, string name, Dictionary<ElementId, OverrideGraphicSettings> elements, ElementId? categoryId,
        OverrideGraphicSettings? category, ElementId template, List<string> problems)
    {
        if (view.IsTemporaryHideIsolateActive())
        {
            problems.Add($"{name}: Reset left temporary hide/isolate on");
        }

        if (view.TemporaryViewModes?.IsModeActive(TemporaryViewMode.TemporaryViewProperties) == true)
        {
            problems.Add($"{name}: Reset left temporary view properties on");
        }

        var changed = elements.Where(pair => !VisibilityRun.Same(view.GetElementOverrides(pair.Key), pair.Value)).Select(pair => pair.Key.ToLong()).ToList();
        if (changed.Count > 0)
        {
            problems.Add($"{name}: Reset did not restore the overrides of {changed.Count} elements");
        }

        if (categoryId is not null && category is not null && !VisibilityRun.Same(view.GetCategoryOverrides(categoryId), category))
        {
            problems.Add($"{name}: Reset did not restore the category overrides");
        }

        if (view.ViewTemplateId != template)
        {
            problems.Add($"{name}: the view template changed");
        }
    }

    private static void Note(List<string> problems, string name, string outcome)
    {
        if (outcome.StartsWith("error", StringComparison.Ordinal))
        {
            problems.Add($"{name}: {outcome}");
        }
    }

    private static string Outcome(Action work)
    {
        try
        {
            work();
            return Ok;
        }
        catch (Exception exception) when (exception.GetType() == typeof(InvalidOperationException))
        {
            return "refused: " + exception.Message;
        }
        catch (Exception exception) when (exception is not AssertionException)
        {
            return $"error: {exception.GetType().FullName}: {exception.Message}";
        }
    }

    private static List<TemplateTarget> Targets(Document host, View view)
    {
        var targets = new List<TemplateTarget>();
        var key = DocumentRegistry.KeyOf(host);
        var inView = new FilteredElementCollector(host, view.Id).WhereElementIsNotElementType()
            .Where(element => element is not RevitLinkInstance && element.Category is { CategoryType: CategoryType.Model })
            .GroupBy(element => element.Category.Id.ToLong())
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();
        if (inView is not null)
        {
            var ids = inView.Take(ProbeSize).Select(element => element.Id).ToList();
            targets.Add(new TemplateTarget($"{ids.Count} {ActiveModel.CategoryName(host, inView.Key)} of the Current model",
                new ElementsStep(key, [.. ids.Select(id => id.ToLong())]), ids, null));
        }

        var link = ActiveModel.Documents.FirstOrDefault(document => !document.Key.IsCurrentModel);
        if (link is null)
        {
            Evidence.Step("No loaded link: the link targets are skipped.");
            return targets;
        }

        var instance = link.Key.LinkInstanceId.ToElementId();
        targets.Add(new TemplateTarget("the link instance", new AttachedModelStep(link.Key), [instance], null));
        var linkCategory = Oracle.Candidates(link.Document, [])
            .GroupBy(element => element.Category.Id.ToLong())
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
        if (linkCategory != 0)
        {
            var name = ActiveModel.CategoryName(link.Document, linkCategory);
            targets.Add(new TemplateTarget($"{name} everywhere", new GroupEverywhereStep(linkCategory, name), [], linkCategory.ToElementId()));
        }

        return targets;
    }

    private static bool LocksGraphics(Document host, View view)
    {
        if (view.ViewTemplateId == ElementId.InvalidElementId || host.GetElement(view.ViewTemplateId) is not View template)
        {
            return false;
        }

        var controlled = new HashSet<long>(template.GetTemplateParameterIds().Select(id => id.ToLong()));
        controlled.ExceptWith(template.GetNonControlledTemplateParameterIds().Select(id => id.ToLong()));
        return controlled.Contains((long)BuiltInParameter.VIS_GRAPHICS_MODEL) && controlled.Contains((long)BuiltInParameter.VIS_GRAPHICS_ANNOTATION);
    }
}
