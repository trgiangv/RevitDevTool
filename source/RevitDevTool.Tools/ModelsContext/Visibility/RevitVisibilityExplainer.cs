using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal sealed class RevitVisibilityExplainer : IVisibilityExplainer
{
    public Task<WhyHidden> ExplainAsync(ElementRef element, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Why hidden", application => Explain(application, element), cancellationToken);
    }

    internal static WhyHidden Explain(UIApplication application, ElementRef element)
    {
        var watch = Stopwatch.StartNew();
        var (active, view) = CurrentView.Require(application);
        var model = active.Document;
        if (!string.Equals(element.Document.ModelId, DocumentRegistry.ModelIdOf(model), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(VisibilityText.ModelChanged);
        }

        var causes = !LinkElements.ShowsLinks(view) ? [HiddenReasons.NotModelView]
            : element.Document.IsCurrentModel ? CurrentCauses(model, view, element.Id)
            : LinkedCauses(model, view, element);
        return WhyHidden.Of(causes, watch.Elapsed.TotalMilliseconds);
    }

    private static List<HiddenReason> CurrentCauses(Document model, View view, long id)
    {
        var element = model.GetElement(id.ToElementId()) ?? throw new InvalidOperationException(VisibilityText.ElementGone);
        var causes = new List<HiddenReason>();
        if (HiddenTemporarily(view, element.Id))
        {
            causes.Add(HiddenReasons.Temporary);
        }

        if (element.IsHidden(view))
        {
            causes.Add(HiddenReasons.InView);
        }

        causes.AddRange(new HiddenChecks(model, view, element, null).Run());
        return causes.Count > 0 ? causes : NestedCauses(model, view, element, null);
    }

    private static List<HiddenReason> LinkedCauses(Document model, View view, ElementRef element)
    {
        if (model.GetElement(element.Document.LinkInstanceId.ToElementId()) is not RevitLinkInstance instance
            || instance.GetLinkDocument() is not { } linkDocument)
        {
            throw new InvalidOperationException(VisibilityText.NotLoaded);
        }

        var linked = linkDocument.GetElement(element.Id.ToElementId()) ?? throw new InvalidOperationException(VisibilityText.ElementGone);
        var causes = new List<HiddenReason>();
        if (instance.IsHidden(view))
        {
            causes.Add(HiddenReasons.LinkInstance);
        }

        if (HiddenTemporarily(view, instance.Id))
        {
            causes.Add(HiddenReasons.LinkTemporary);
        }

        if (instance.Category is { } linkCategory && HiddenChecks.IsCategoryHidden(view, linkCategory.Id))
        {
            causes.Add(HiddenReasons.LinkCategory(linkCategory.Name));
        }

        if (HiddenChecks.HiddenWorksetName(model, view, instance.WorksetId) is { } workset)
        {
            causes.Add(HiddenReasons.LinkWorkset(workset));
        }

        if (DisplayModeOf(view, instance) is { } mode)
        {
            causes.Add(HiddenReasons.LinkDisplayMode(mode));
            return causes;
        }

        causes.AddRange(new HiddenChecks(model, view, linked, instance).Run());
        return causes.Count > 0 ? causes : NestedCauses(model, view, linked, instance);
    }

    private static List<HiddenReason> NestedCauses(Document model, View view, Element element, RevitLinkInstance? link)
    {
        if (element is not FamilyInstance { SuperComponent: FamilyInstance parent } || IsDrawn(model, view, element.Id, link))
        {
            return [];
        }

        var causes = new HiddenChecks(model, view, parent, link).Run().ToList();
        return causes.Count > 0 ? causes : [HiddenReasons.FamilySettings(parent.Symbol.FamilyName)];
    }

    private static bool IsDrawn(Document model, View view, ElementId id, RevitLinkInstance? link)
    {
        return HiddenChecks.ViewCollector(model, view, link)?.WherePasses(new ElementIdSetFilter([id])).Any() == true;
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
}
