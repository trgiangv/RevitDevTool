using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class VisibilityRun
{
    private const int ProbeSize = 25;

    private static Adapter _adapter = null!;
    private static RevitViewActions _actions = null!;

    private static UIDocument Active => SampleViews.Active;

    private static View View => Active.ActiveView;

    public static void Open()
    {
        _adapter = new Adapter();
        _actions = new RevitViewActions();
    }

    public static void Check(VisibilityCase item)
    {
        SampleViews.Switch(SampleViews.ThreeD);
        Reset();
        var expect = item.Expect;
        var probe = Probe(item);
        var plan = Plan(item, probe);
        Evidence.Step($"Made '{View.Name}' the active view through UIDocument.ActiveView (no model edit).");
        Evidence.Step($"Built the {item.Tier} plan the panel sends for a {item.Source} {item.Action}: {Describe(plan)}.");
        Shot("before");

        if (expect.PlannedTiers is { } tiers)
        {
            var planned = Tiers(plan);
            Evidence.Step("Link elements go through Hide in View (T3, an internal Revit command). L2 checks the plan only and never runs it: see Gotchas in docs/architecture/ModelsContext/README.md.");
            Shot("after");
            Evidence.Values(string.Join(" + ", tiers), string.Join(" + ", planned));
            Assert.That(planned, Is.EqualTo(tiers));
            return;
        }

        if (expect.Error is { } error)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _actions.ExecuteAsync(plan, CancellationToken.None));
            Evidence.Step("Sent the plan to RevitViewActions.ExecuteAsync; it was refused.");
            Shot("after");
            Evidence.Values(error, exception!.Message);
            Assert.That(exception.Message, Is.EqualTo(error));
            Assert.That(View.IsTemporaryHideIsolateActive(), Is.False, "The refused action changed the view.");
            return;
        }

        Assert.That(plan.UsesReferenceCommands, Is.False, "L2 never runs a T3 plan (Hide in View through an internal command); declare plannedTiers in visibility.json.");
        var saved = Overrides(plan);
        VisibilityState after;
        VisibilityState reset;
        try
        {
            Evidence.Time("panel", () => Adapter.Run(_actions.ExecuteAsync(plan, CancellationToken.None)));
            Evidence.Step("Sent the plan to RevitViewActions.ExecuteAsync (the adapter the panel calls).");
            after = Evidence.Time("oracle", () => Read(probe, item.Color));
            Shot("after");
        }
        finally
        {
            Evidence.Time("reset", Reset);
            Evidence.Step("Reset: RevitViewActions.ResetAsync.");
            Shot("reset");
            reset = Read(probe, item.Color);
        }

        Evidence.Values(item.ExpectedText, after.ToString());
        Evidence.Step($"After reset: {reset}.");
        Verify(expect, after);
        Assert.That(reset.TemporaryHideIsolate || reset.TemporaryProperties, Is.False, "Reset left a temporary mode on.");
        Assert.That(reset.TargetColored || reset.OtherColored, Is.False, "Reset left a color override.");
        Assert.That(Changed(saved), Is.Empty, "Reset did not restore the element overrides the view had before.");
        Assert.That(reset.LinkedVisible, Is.Not.EqualTo(false), "Reset left linked elements hidden.");
        Assert.That(reset.LinkVisible, Is.Not.EqualTo(false), "Reset left the link hidden.");
    }

    private static Dictionary<ElementId, OverrideGraphicSettings> Overrides(VisibilityPlan plan)
    {
        if (plan.Action is not (VisibilityAction.Color or VisibilityAction.Halftone))
        {
            return [];
        }

        var ids = plan.Steps.OfType<ElementsStep>().SelectMany(step => step.Ids)
            .Concat(plan.Steps.OfType<AttachedModelStep>().Select(step => step.Document.LinkInstanceId));
        return ids.Distinct().Select(id => id.ToElementId()).ToDictionary(id => id, id => View.GetElementOverrides(id));
    }

    private static List<long> Changed(Dictionary<ElementId, OverrideGraphicSettings> saved)
    {
        var view = View;
        return saved.Where(pair => !Same(view.GetElementOverrides(pair.Key), pair.Value)).Select(pair => pair.Key.ToLong()).ToList();
    }

    private static bool Same(OverrideGraphicSettings left, OverrideGraphicSettings right)
    {
        return left.Halftone == right.Halftone
               && SameColor(left.ProjectionLineColor, right.ProjectionLineColor)
               && SameColor(left.SurfaceForegroundPatternColor, right.SurfaceForegroundPatternColor)
               && left.SurfaceForegroundPatternId == right.SurfaceForegroundPatternId;
    }

    private static bool SameColor(Color left, Color right)
    {
        return left.IsValid == right.IsValid && (!left.IsValid || (left.Red == right.Red && left.Green == right.Green && left.Blue == right.Blue));
    }

    private static List<string> Tiers(VisibilityPlan plan)
    {
        return plan.Steps.Select(step => step switch
        {
            ElementsStep => "T0",
            AttachedModelStep => "T1",
            GroupEverywhereStep => "T2",
            ReferenceCommandStep => "T3",
            _ => step.GetType().Name
        }).Distinct().ToList();
    }

    private static void Verify(VisibilityExpect expect, VisibilityState actual)
    {
        var problems = new List<string>();
        Compare(problems, "temporary hide/isolate", expect.TemporaryHideIsolate, actual.TemporaryHideIsolate);
        Compare(problems, "temporary view properties", expect.TemporaryProperties, actual.TemporaryProperties);
        Compare(problems, "target visible", expect.TargetVisible, actual.TargetVisible);
        Compare(problems, "other visible", expect.OtherVisible, actual.OtherVisible);
        Compare(problems, "link visible", expect.LinkVisible, actual.LinkVisible);
        Compare(problems, "linked elements visible", expect.LinkedVisible, actual.LinkedVisible);
        Compare(problems, "target colored", expect.TargetColor, actual.TargetColored);
        Compare(problems, "other colored", expect.OtherColor, actual.OtherColored);
        Assert.That(problems, Is.Empty, string.Join("; ", problems));
    }

    private static void Compare(List<string> problems, string name, bool? expected, bool? actual)
    {
        if (expected is { } wanted && actual != wanted)
        {
            problems.Add($"{name}: expected {wanted}, got {actual?.ToString() ?? "mixed"}");
        }
    }

    private static Probe Probe(VisibilityCase item)
    {
        var inView = new HashSet<long>(new FilteredElementCollector(SampleModel.Host, View.Id).WhereElementIsNotElementType()
            .ToElementIds().Select(id => id.ToLong()));
        var link = SampleModel.Architectural;
        var target = Elements(item.Target);
        var other = item.Other is null ? [] : Elements(item.Other);
        var linked = target.Where(element => !element.Document.IsCurrentModel)
            .Concat(item.Linked is null ? [] : Elements(item.Linked).Where(element => !element.Document.IsCurrentModel))
            .Distinct()
            .ToList();
        return new Probe(
            target,
            Sample(target, inView),
            Sample(other, inView),
            linked,
            link.Key,
            item.Target.Link,
            item.Target.Everywhere ? Cases.Category(item.Target.Category!) : null);
    }

    private static List<ElementRef> Elements(TargetData target)
    {
        if (target.Link)
        {
            return [];
        }

        if (target.FilterCase is { } id)
        {
            var filter = CaseFiles.Filter(id);
            return _adapter.Matches(Cases.Definition(filter), SampleModel.Of(filter.Docs), ScopeKind.WholeModel);
        }

        var document = SampleModel.Of(target.Docs ?? "Host")[0];
        return Oracle.Candidates(document.Document, [Cases.Category(target.Category!)])
            .Select(element => new ElementRef(document.Key, element.Id.ToLong()))
            .ToList();
    }

    private static List<ElementId> Sample(IEnumerable<ElementRef> elements, HashSet<long> inView)
    {
        return elements.Where(element => element.Document.IsCurrentModel && inView.Contains(element.Id))
            .Take(ProbeSize)
            .Select(element => element.Id.ToElementId())
            .ToList();
    }

    private static VisibilityPlan Plan(VisibilityCase item, Probe probe)
    {
        var action = (VisibilityAction)Enum.Parse(typeof(VisibilityAction), item.Action);
        RgbColor? color = item.Color is { } rgb ? new RgbColor(rgb.R, rgb.G, rgb.B) : null;
        var steps = new List<VisibilityStep>();
        if (probe.EverywhereCategory is { } category)
        {
            steps.Add(new GroupEverywhereStep(category, CaseFiles.Facts.CategoryNames.TryGetValue(item.Target.Category!, out var name) ? name : item.Target.Category!));
        }
        else if (probe.WholeLink)
        {
            steps.Add(new AttachedModelStep(probe.Link));
        }
        else
        {
            foreach (var group in probe.Target.GroupBy(element => element.Document).OrderBy(group => !group.Key.IsCurrentModel))
            {
                var ids = group.Select(element => element.Id).OrderBy(value => value).ToList();
                steps.Add(group.Key.IsCurrentModel ? new ElementsStep(group.Key, ids) : new ReferenceCommandStep(group.Key, ids));
            }
        }

        return new VisibilityPlan(action, color, steps, null);
    }

    private static VisibilityState Read(Probe probe, RgbData? color)
    {
        var view = View;
        var hideIsolate = view.IsTemporaryHideIsolateActive();
        var properties = view.TemporaryViewModes?.IsModeActive(TemporaryViewMode.TemporaryViewProperties) == true;
        bool? linkVisible = IsVisible(view, SampleModel.Architectural.Key.LinkInstanceId.ToElementId(), hideIsolate);
        bool? linkedVisible = null;
        if (probe.Linked.Count > 0)
        {
            var visible = LinkElements.VisibleOf(SampleModel.Host, view, probe.Linked).Count;
            linkedVisible = visible == probe.Linked.Count ? true : visible == 0 ? false : null;
        }

        return new VisibilityState(
            hideIsolate,
            properties,
            All(probe.TargetIds.Select(id => IsVisible(view, id, hideIsolate))),
            All(probe.OtherIds.Select(id => IsVisible(view, id, hideIsolate))),
            linkVisible,
            linkedVisible,
            color is not null && TargetColored(view, probe, color),
            color is not null && probe.OtherIds.Any(id => Matches(view.GetElementOverrides(id), color)));
    }

    private static bool TargetColored(View view, Probe probe, RgbData color)
    {
        if (probe.EverywhereCategory is { } category)
        {
            return Matches(view.GetCategoryOverrides(category.ToElementId()), color);
        }

        if (probe.WholeLink)
        {
            return Matches(view.GetElementOverrides(probe.Link.LinkInstanceId.ToElementId()), color);
        }

        return probe.TargetIds.Count > 0 && probe.TargetIds.All(id => Matches(view.GetElementOverrides(id), color));
    }

    private static bool Matches(OverrideGraphicSettings settings, RgbData color)
    {
        var line = settings.ProjectionLineColor;
        return line.IsValid && line.Red == color.R && line.Green == color.G && line.Blue == color.B;
    }

    private static bool IsVisible(View view, ElementId id, bool temporary)
    {
        return SampleModel.Host.GetElement(id) is { } element
               && !element.IsHidden(view)
               && (!temporary || view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, id));
    }

    private static bool? All(IEnumerable<bool> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return list.All(value => value) ? true : list.Any(value => value) ? null : false;
    }

    private static void Reset()
    {
        Adapter.Run(_actions.ResetAsync(CancellationToken.None));
    }

    private static void Shot(string label)
    {
        Evidence.Image(label, path => ViewImage.Export(SampleModel.Host, View, path));
    }

    private static string Describe(VisibilityPlan plan)
    {
        return $"{plan.Action}{(plan.Color is { } color ? $" #{color.R:X2}{color.G:X2}{color.B:X2}" : "")} with " + string.Join(", ", plan.Steps.Select(step => step switch
        {
            GroupEverywhereStep group => $"category everywhere '{group.GroupName}'",
            AttachedModelStep => "the Architectural link instance",
            ElementsStep elements => $"{elements.Ids.Count} Current model elements",
            ReferenceCommandStep references => $"{references.Ids.Count} linked elements",
            _ => step.GetType().Name
        }));
    }
}

internal sealed record Probe(
    IReadOnlyList<ElementRef> Target,
    IReadOnlyList<ElementId> TargetIds,
    IReadOnlyList<ElementId> OtherIds,
    IReadOnlyList<ElementRef> Linked,
    DocumentKey Link,
    bool WholeLink,
    long? EverywhereCategory);

internal sealed record VisibilityState(
    bool TemporaryHideIsolate,
    bool TemporaryProperties,
    bool? TargetVisible,
    bool? OtherVisible,
    bool? LinkVisible,
    bool? LinkedVisible,
    bool TargetColored,
    bool OtherColored)
{
    public override string ToString()
    {
        return $"hide/isolate {TemporaryHideIsolate}, view properties {TemporaryProperties}, target visible {Show(TargetVisible)}, other visible {Show(OtherVisible)}, link visible {Show(LinkVisible)}, linked visible {Show(LinkedVisible)}, target colored {TargetColored}, other colored {OtherColored}";
    }

    private static string Show(bool? value)
    {
        return value?.ToString() ?? "mixed/none";
    }
}
