using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal sealed class RevitVisibilityExplainer : IVisibilityExplainer
{
    public const string ElementGone = "This element is no longer in the model.";

    public Task<WhyHiddenResult> ExplainAsync(ElementRef element, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Why hidden", application => Explain(application, element), cancellationToken);
    }

    internal static WhyHiddenResult Explain(UIApplication application, ElementRef element)
    {
        var watch = Stopwatch.StartNew();
        var (active, view) = RevitViewActions.RequireView(application);
        var model = active.Document;
        if (!string.Equals(element.Document.ModelId, DocumentRegistry.ModelIdOf(model), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(RevitViewActions.ModelChanged);
        }

        var causes = !IsModelView(view) ? [HiddenCues.NotModelView]
            : element.Document.IsCurrentModel ? CurrentCauses(model, view, element.Id)
            : LinkedCauses(model, view, element);
        return WhyHiddenResult.Of(causes, watch.Elapsed.TotalMilliseconds);
    }

    private static List<HiddenCause> CurrentCauses(Document model, View view, long id)
    {
        var element = model.GetElement(id.ToElementId()) ?? throw new InvalidOperationException(ElementGone);
        var causes = new List<HiddenCause>();
        if (HiddenTemporarily(view, element.Id))
        {
            causes.Add(HiddenCues.TemporaryHideIsolate);
        }

        if (element.IsHidden(view))
        {
            causes.Add(HiddenCues.HiddenInView);
        }

        causes.AddRange(new HiddenChecks(model, view, element, null).Run());
        return causes.Count > 0 ? causes : NestedCauses(model, view, element, null);
    }

    private static List<HiddenCause> LinkedCauses(Document model, View view, ElementRef element)
    {
        if (model.GetElement(element.Document.LinkInstanceId.ToElementId()) is not RevitLinkInstance instance
            || instance.GetLinkDocument() is not { } linkDocument)
        {
            throw new InvalidOperationException(VisibilityReasons.NotLoaded);
        }

        var linked = linkDocument.GetElement(element.Id.ToElementId()) ?? throw new InvalidOperationException(ElementGone);
        var causes = new List<HiddenCause>();
        if (instance.IsHidden(view))
        {
            causes.Add(HiddenCues.LinkHidden);
        }

        if (HiddenTemporarily(view, instance.Id))
        {
            causes.Add(HiddenCues.LinkTemporary);
        }

        if (instance.Category is { } linkCategory && HiddenChecks.IsCategoryHidden(view, linkCategory.Id))
        {
            causes.Add(HiddenCues.LinkCategory(linkCategory.Name));
        }

        if (HiddenChecks.HiddenWorksetName(model, view, instance.WorksetId) is { } workset)
        {
            causes.Add(HiddenCues.LinkWorkset(workset));
        }

        if (DisplayModeOf(view, instance) is { } mode)
        {
            causes.Add(HiddenCues.LinkDisplayMode(mode));
            return causes;
        }

        causes.AddRange(new HiddenChecks(model, view, linked, instance).Run());
        return causes.Count > 0 ? causes : NestedCauses(model, view, linked, instance);
    }

    private static List<HiddenCause> NestedCauses(Document model, View view, Element element, RevitLinkInstance? link)
    {
        if (element is not FamilyInstance { SuperComponent: FamilyInstance parent } || IsDrawn(model, view, element.Id, link))
        {
            return [];
        }

        var causes = new HiddenChecks(model, view, parent, link).Run().ToList();
        return causes.Count > 0 ? causes : [HiddenCues.FamilySettings(parent.Symbol.FamilyName)];
    }

    private static bool IsDrawn(Document model, View view, ElementId id, RevitLinkInstance? link)
    {
        var collector = link is null
            ? new FilteredElementCollector(model, view.Id)
            : new FilteredElementCollector(model, view.Id, link.Id);
        return collector.WherePasses(new ElementIdSetFilter([id])).Any();
    }

    private static bool HiddenTemporarily(View view, ElementId id)
    {
        return view.IsTemporaryHideIsolateActive()
               && !view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, id);
    }

    internal static string? DisplayModeOf(View view, RevitLinkInstance instance)
    {
#if REVIT2024_OR_GREATER
        return view.GetLinkOverrides(instance.Id)?.LinkVisibilityType switch
        {
            LinkVisibility.ByLinkView => "By Linked View",
            LinkVisibility.Custom => "Custom",
            _ => null
        };
#else
        return null;
#endif
    }

    private static bool IsModelView(View view)
    {
        return !view.IsTemplate && view.ViewType is ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan
            or ViewType.AreaPlan or ViewType.Section or ViewType.Elevation or ViewType.Detail or ViewType.ThreeD
            or ViewType.Walkthrough;
    }
}
