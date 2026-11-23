using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal sealed class HiddenChecks(Document model, View view, Element element, RevitLinkInstance? link)
{
    private const double Tolerance = 1e-6;

    private static readonly HashSet<long> ShownAboveCut =
    [
        .. new[]
        {
            BuiltInCategory.OST_Windows, BuiltInCategory.OST_Casework, BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_FlexDuctCurves, BuiltInCategory.OST_DuctInsulations,
            BuiltInCategory.OST_DuctLinings, BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory, BuiltInCategory.OST_FlexPipeCurves, BuiltInCategory.OST_PipeInsulations,
            BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting, BuiltInCategory.OST_Conduit,
            BuiltInCategory.OST_ConduitFitting, BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_Sprinklers, BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_ElectricalEquipment
        }.Select(category => (long)category)
    ];

    private Document Source => element.Document;

    private bool Linked => link is not null;

    public IEnumerable<HiddenCause> Run()
    {
        Func<HiddenCause?>[] checks = [Category, Filter, Workset, Phase, DesignOption, OwnerView, NotDrawn, ViewRange, Crop];
        foreach (var check in checks)
        {
            if (Try(check) is { } cause)
            {
                yield return cause;
            }
        }
    }

    public static bool IsCategoryHidden(View view, ElementId categoryId)
    {
        try
        {
            return view.GetCategoryHidden(categoryId);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    public static string? HiddenWorksetName(Document document, View view, WorksetId worksetId)
    {
        if (!document.IsWorkshared || worksetId == WorksetId.InvalidWorksetId)
        {
            return null;
        }

        try
        {
            var visibility = view.GetWorksetVisibility(worksetId);
            var hidden = visibility == WorksetVisibility.Hidden
                         || (visibility == WorksetVisibility.UseGlobalSetting
                             && !WorksetDefaultVisibilitySettings.GetWorksetDefaultVisibilitySettings(document).IsWorksetVisible(worksetId));
            return hidden ? document.GetWorksetTable().GetWorkset(worksetId).Name : null;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return null;
        }
    }

    private static HiddenCause? Try(Func<HiddenCause?> check)
    {
        try
        {
            return check();
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return null;
        }
    }

    private HiddenCause? Category()
    {
        for (var category = LineStyleOf(element) ?? element.Category; category is not null; category = category.Parent)
        {
            if (HostCategoryId(category) is { } id && IsCategoryHidden(view, id))
            {
                return HiddenCues.Category(category.Parent is { } parent ? $"{parent.Name}: {category.Name}" : category.Name);
            }
        }

        return null;
    }

    private static Category? LineStyleOf(Element element)
    {
        try
        {
            return element is CurveElement { LineStyle: GraphicsStyle style } ? style.GraphicsStyleCategory : null;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return null;
        }
    }

    private ElementId? HostCategoryId(Category category)
    {
        if (!Linked || category.Id.ToLong() < 0)
        {
            return category.Id;
        }

        return category.Parent is { } parent && parent.Id.ToLong() < 0
                                              && Autodesk.Revit.DB.Category.GetCategory(model, parent.Id) is { } hostParent
                                              && hostParent.SubCategories.Contains(category.Name)
            ? hostParent.SubCategories.get_Item(category.Name).Id
            : null;
    }

    private HiddenCause? Filter()
    {
        foreach (var filterId in view.GetFilters())
        {
            if (view.GetFilterVisibility(filterId) || !view.GetIsFilterEnabled(filterId)
                || model.GetElement(filterId) is not FilterElement filter)
            {
                continue;
            }

            if ((Linked ? LinkedFilter(filter) : CurrentFilter(filter)) is { } cause)
            {
                return cause;
            }
        }

        return null;
    }

    private HiddenCause? CurrentFilter(FilterElement filter)
    {
        switch (filter)
        {
            case SelectionFilterElement selection:
                return selection.GetElementIds().Contains(element.Id) ? HiddenCues.Filter(filter.Name) : null;
            case ParameterFilterElement parameters when InCategories(parameters):
                try
                {
                    return parameters.GetElementFilter() is not { } rules || rules.PassesFilter(element)
                        ? HiddenCues.Filter(filter.Name)
                        : null;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    return HiddenCues.FilterPossible(filter.Name);
                }
            default:
                return null;
        }
    }

    private HiddenCause? LinkedFilter(FilterElement filter)
    {
        return filter is ParameterFilterElement parameters && InCategories(parameters)
            ? HiddenCues.FilterPossible(filter.Name)
            : null;
    }

    private bool InCategories(ParameterFilterElement filter)
    {
        return element.Category is { } category && HostCategoryId(category) is { } id && filter.GetCategories().Contains(id);
    }

    private HiddenCause? Workset()
    {
        var source = Source;
        if (!source.IsWorkshared || element.WorksetId == WorksetId.InvalidWorksetId)
        {
            return null;
        }

        var workset = source.GetWorksetTable().GetWorkset(element.WorksetId);
        if (!workset.IsOpen)
        {
            return HiddenCues.WorksetClosed(workset.Name);
        }

        return !Linked && HiddenWorksetName(source, view, element.WorksetId) is { } name ? HiddenCues.WorksetHidden(name) : null;
    }

    private HiddenCause? Phase()
    {
        if (!element.HasPhases()
            || view.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId() is not { } viewPhaseId
            || model.GetElement(viewPhaseId) is not Autodesk.Revit.DB.Phase viewPhase)
        {
            return null;
        }

        var phases = Source.Phases.Cast<Autodesk.Revit.DB.Phase>().ToList();
        var viewIndex = Linked
            ? phases.FindIndex(phase => string.Equals(phase.Name, viewPhase.Name, StringComparison.Ordinal))
            : phases.FindIndex(phase => phase.Id == viewPhase.Id);
        if (viewIndex < 0)
        {
            return null;
        }

        var created = phases.FindIndex(phase => phase.Id == element.CreatedPhaseId);
        if (created > viewIndex)
        {
            return HiddenCues.CreatedLater(phases[created].Name);
        }

        var demolished = phases.FindIndex(phase => phase.Id == element.DemolishedPhaseId);
        return demolished >= 0 && demolished <= viewIndex ? HiddenCues.Demolished(phases[demolished].Name) : null;
    }

    private HiddenCause? DesignOption()
    {
        if (element.DesignOption is not { IsPrimary: false } option)
        {
            return null;
        }

        return !Linked && Autodesk.Revit.DB.DesignOption.GetActiveDesignOptionId(model) == option.Id
            ? null
            : HiddenCues.DesignOption(option.Name);
    }

    private HiddenCause? OwnerView()
    {
        var owner = element.OwnerViewId;
        if (owner == ElementId.InvalidElementId || (!Linked && owner == view.Id))
        {
            return null;
        }

        return HiddenCues.OwnerView(Source.GetElement(owner)?.Name ?? owner.ToString());
    }

    private HiddenCause? NotDrawn()
    {
        if (element.OwnerViewId != ElementId.InvalidElementId)
        {
            return null;
        }

        var draw = ElementDrawing.DrawOf(element);
        if (draw == ViewDraw.None)
        {
            return HiddenCues.NoGeometry;
        }

        return (draw & ViewDraws.Of(ElementDrawing.KindOf(view))) == 0 ? HiddenCues.NotInViewType : null;
    }

    private HiddenCause? ViewRange()
    {
        if (view is not ViewPlan plan || Corners() is not { Count: > 0 } corners)
        {
            return null;
        }

        var range = plan.GetViewRange();
        var minZ = corners.Min(point => point.Z);
        var maxZ = corners.Max(point => point.Z);
        return plan.ViewType == ViewType.CeilingPlan
            ? CeilingRange(plan, range, minZ, maxZ)
            : PlanRange(plan, range, minZ, maxZ);
    }

    private HiddenCause? CeilingRange(ViewPlan plan, PlanViewRange range, double minZ, double maxZ)
    {
        if (Elevation(plan, range, PlanViewPlane.CutPlane) is { } cut && maxZ < cut - Tolerance)
        {
            return HiddenCues.BelowCutPlane;
        }

        var top = Elevation(plan, range, PlanViewPlane.TopClipPlane);
        var depth = Elevation(plan, range, PlanViewPlane.ViewDepthPlane);
        var limit = top is { } t && depth is { } d ? Math.Max(t, d) : top ?? depth;
        return limit is { } above && minZ > above + Tolerance ? HiddenCues.AboveViewRange : null;
    }

    private HiddenCause? PlanRange(ViewPlan plan, PlanViewRange range, double minZ, double maxZ)
    {
        if (Elevation(plan, range, PlanViewPlane.TopClipPlane) is { } top && minZ > top + Tolerance)
        {
            return HiddenCues.AboveViewRange;
        }

        if (Elevation(plan, range, PlanViewPlane.CutPlane) is { } cut && minZ > cut + Tolerance && !IsShownAboveCut())
        {
            return HiddenCues.AboveCutPlane;
        }

        return Elevation(plan, range, PlanViewPlane.ViewDepthPlane) is { } depth && maxZ < depth - Tolerance
            ? HiddenCues.BelowViewDepth
            : null;
    }

    private bool IsShownAboveCut()
    {
        return element.Category is { } category && ShownAboveCut.Contains(category.Id.ToLong());
    }

    private double? Elevation(ViewPlan plan, PlanViewRange range, PlanViewPlane plane)
    {
        var levelId = range.GetLevelId(plane);
        var level = model.GetElement(levelId) as Level ?? (levelId == PlanViewRange.Current ? plan.GenLevel : null);
        return level is null ? null : level.ProjectElevation + range.GetOffset(plane);
    }

    private HiddenCause? Crop()
    {
        if (Corners() is not { Count: > 0 } corners)
        {
            return null;
        }

        if (view is View3D { IsSectionBoxActive: true } sectioned)
        {
            return Outside(sectioned.GetSectionBox(), corners, withDepth: true) ? HiddenCues.OutsideSectionBox : null;
        }

        if (!view.CropBoxActive || view is View3D { IsPerspective: true })
        {
            return null;
        }

        return Outside(view.CropBox, corners, withDepth: false) ? HiddenCues.OutsideCrop : null;
    }

    private static bool Outside(BoundingBoxXYZ box, List<XYZ> corners, bool withDepth)
    {
        var inverse = box.Transform.Inverse;
        var local = corners.Select(inverse.OfPoint).ToList();
        return local.Max(point => point.X) < box.Min.X - Tolerance || local.Min(point => point.X) > box.Max.X + Tolerance
            || local.Max(point => point.Y) < box.Min.Y - Tolerance || local.Min(point => point.Y) > box.Max.Y + Tolerance
            || (withDepth && (local.Max(point => point.Z) < box.Min.Z - Tolerance
                              || local.Min(point => point.Z) > box.Max.Z + Tolerance));
    }

    private List<XYZ>? Corners()
    {
        if (element.get_BoundingBox(null) is not { } box)
        {
            return null;
        }

        var transform = link is null ? box.Transform : link.GetTotalTransform().Multiply(box.Transform);
        var corners = new List<XYZ>(8);
        foreach (var x in new[] { box.Min.X, box.Max.X })
        {
            foreach (var y in new[] { box.Min.Y, box.Max.Y })
            {
                foreach (var z in new[] { box.Min.Z, box.Max.Z })
                {
                    corners.Add(transform.OfPoint(new XYZ(x, y, z)));
                }
            }
        }

        return corners;
    }
}
