using RevitDevTool.Core;
using RevitDevTool.Tools.Geometry;

namespace RevitDevTool.Tools.Selection;

/// <summary>
/// Present search matches in the active UIDocument.
/// Flow: collect → expand host IndependentTags into linked groups → set selection → zoom.
/// </summary>
public static class ElementSelector
{
    /// <summary>
    /// Select on the active UIDocument; optional section box via host Raise.
    /// </summary>
    public static void Select(IReadOnlyList<SearchMatch> matches, bool zoom, bool sectionBox)
    {
        var uiDoc = RevitContext.ActiveUiDocument;
        if (uiDoc is null || matches.Count == 0)
            return;

        Select(uiDoc, matches, zoom);

        if (!sectionBox)
            return;

        RevitContextExecutor.Raise(uiApp =>
        {
            var active = uiApp.ActiveUIDocument;
            if (active is null)
                return;
            // One SetSectionBox for the union of all matches (per-match would overwrite).
            ApplySectionBox(active, matches);
        });
    }

    public static int CountElements(IReadOnlyList<SearchMatch> matches)
    {
        var count = 0;
        foreach (var match in matches)
        {
            count += match switch
            {
                SearchMatch.HostElements host => host.Ids.Count,
                SearchMatch.LinkedElements linked => linked.Ids.Count,
                _ => 0
            };
        }

        return count;
    }

    public static void Select(UIDocument uiDocument, SearchMatch match, bool zoom = true)
    {
        ArgumentNullException.ThrowIfNull(uiDocument);
        ArgumentNullException.ThrowIfNull(match);
        Select(uiDocument, [match], zoom);
    }

    public static void Select(UIDocument uiDocument, IReadOnlyList<SearchMatch> matches, bool zoom = true)
    {
        ArgumentNullException.ThrowIfNull(uiDocument);
        ArgumentNullException.ThrowIfNull(matches);
        if (matches.Count == 0)
            return;

        try
        {
            Collect(matches, out var hostIds, out var linkedGroups);
            ExpandTaggedLinks(uiDocument.Document, hostIds, ref linkedGroups);
            Present(uiDocument, hostIds, linkedGroups, zoom);
        }
        catch
        {
            // Monitor / tool click is silent on failure.
        }
    }

    private struct LinkedGroup(RevitLinkInstance instance)
    {
        public readonly RevitLinkInstance Instance = instance;
        public readonly List<Element> Elements = [];
    }

    // --- collect ---

    private static void Collect(
        IReadOnlyList<SearchMatch> matches,
        out List<ElementId> hostIds,
        out List<LinkedGroup> linkedGroups)
    {
        hostIds = [];
        Dictionary<ElementId, LinkedGroup>? byInstance = null;

        foreach (var match in matches)
        {
            switch (match)
            {
                case SearchMatch.HostElements host:
                    hostIds.AddRange(host.Ids);
                    break;
                case SearchMatch.LinkedElements linked:
                    AddLinked(ref byInstance, linked.Instance, linked.Ids);
                    break;
            }
        }

        linkedGroups = byInstance is null ? [] : [..byInstance.Values];
    }

    private static void AddLinked(
        ref Dictionary<ElementId, LinkedGroup>? byInstance,
        RevitLinkInstance instance,
        IEnumerable<ElementId> ids)
    {
        var linkDoc = instance.GetLinkDocument();
        if (linkDoc is null)
            return;

        byInstance ??= [];
        if (!byInstance.TryGetValue(instance.Id, out var group))
        {
            group = new LinkedGroup(instance);
            byInstance[instance.Id] = group;
        }

        foreach (var id in ids)
        {
            var element = linkDoc.GetElement(id);
            if (element is not null)
                group.Elements.Add(element);
        }
    }

    /// <summary>
    /// Host IndependentTags that point into links contribute those targets to <paramref name="linkedGroups"/>.
    /// </summary>
    private static void ExpandTaggedLinks(
        Document document,
        List<ElementId> hostIds,
        ref List<LinkedGroup> linkedGroups)
    {
        if (hostIds.Count == 0)
            return;

        var byInstance = IndexByInstance(linkedGroups);
        foreach (var id in hostIds)
            TryExpandTag(document, id, ref byInstance);

        if (byInstance is not null)
            linkedGroups = [..byInstance.Values];
    }

    private static Dictionary<ElementId, LinkedGroup>? IndexByInstance(List<LinkedGroup> linkedGroups)
    {
        if (linkedGroups.Count == 0)
            return null;

        var byInstance = new Dictionary<ElementId, LinkedGroup>(linkedGroups.Count);
        foreach (var group in linkedGroups)
            byInstance[group.Instance.Id] = group;
        return byInstance;
    }

    private static void TryExpandTag(
        Document document,
        ElementId hostId,
        ref Dictionary<ElementId, LinkedGroup>? byInstance)
    {
        if (document.GetElement(hostId) is not IndependentTag tag)
            return;

        foreach (var linkElementId in tag.GetTaggedElementIds())
            TryAddTaggedLink(document, linkElementId, ref byInstance);
    }

    private static void TryAddTaggedLink(
        Document document,
        LinkElementId linkElementId,
        ref Dictionary<ElementId, LinkedGroup>? byInstance)
    {
        if (linkElementId.LinkInstanceId == ElementId.InvalidElementId)
            return;
        if (document.GetElement(linkElementId.LinkInstanceId) is not RevitLinkInstance instance)
            return;

        AddLinked(ref byInstance, instance, [linkElementId.LinkedElementId]);
    }

    // --- present ---

    private static void Present(
        UIDocument uiDocument,
        List<ElementId> hostIds,
        List<LinkedGroup> linkedGroups,
        bool zoom)
    {
        if (!TryPrepareHostView(uiDocument, hostIds))
            return;

        var hasHost = hostIds.Count > 0;
        var hasLinked = linkedGroups.Exists(static g => g.Elements.Count > 0);

#if REVIT2023_OR_GREATER
        if (hasLinked)
        {
            TrySetReferences(uiDocument, ToReferences(uiDocument.Document, hostIds, linkedGroups));
            if (!zoom)
                return;
            if (hasHost)
                uiDocument.ShowElements(ResolveVisibleIds(uiDocument.Document, hostIds));
            ZoomLinked(uiDocument, linkedGroups);
            return;
        }
#else
        // Pre-2023: cannot select linked elements; zoom only when there is no host.
        if (hasLinked && !hasHost)
        {
            if (zoom)
                ZoomLinked(uiDocument, linkedGroups);
            return;
        }
#endif

        if (!hasHost)
            return;

        uiDocument.Selection.SetElementIds(hostIds);
        if (zoom)
            uiDocument.ShowElements(ResolveVisibleIds(uiDocument.Document, hostIds));
    }

    /// <summary>
    /// Returns false when selection should stop (e.g. switched into a View element).
    /// </summary>
    private static bool TryPrepareHostView(UIDocument uiDocument, List<ElementId> hostIds)
    {
        if (hostIds.Count == 0)
            return true;

        var document = uiDocument.Document;
        var first = document.GetElement(hostIds[0]);
        switch (first)
        {
            case null:
                return false;
            case View view:
                uiDocument.RequestViewChange(view);
                return false;
        }

        if (first.OwnerViewId != ElementId.InvalidElementId && HasSameOwnerView(document, hostIds))
        {
            var ownerView = (View)document.GetElement(first.OwnerViewId);
            uiDocument.RequestViewChange(ownerView);
        }

        return true;
    }

#if REVIT2023_OR_GREATER
    private static List<Reference> ToReferences(
        Document document,
        List<ElementId> hostIds,
        IReadOnlyList<LinkedGroup> linkedGroups)
    {
        var references = new List<Reference>();

        foreach (var id in hostIds)
        {
            if (document.GetElement(id) is { } element)
                references.Add(new Reference(element));
        }

        foreach (var group in linkedGroups)
        {
            foreach (var element in group.Elements)
                references.Add(new Reference(element).CreateLinkReference(group.Instance));
        }

        return references;
    }

    private static void TrySetReferences(UIDocument uiDocument, List<Reference> references)
    {
        if (references.Count == 0)
            return;

        try
        {
            uiDocument.Selection.SetReferences(references);
        }
        catch
        {
            // Selection may fail (API/view); still zoom when requested.
        }
    }
#endif

    private static void ZoomLinked(UIDocument uiDocument, IReadOnlyList<LinkedGroup> groups)
    {
        var view = uiDocument.ActiveView;
        if (view is null)
            return;

        foreach (var group in groups)
        {
            if (group.Elements.Count == 0)
                continue;

            try
            {
                if (!ElementBox.TryBox(
                        group.Elements, group.Instance, view, 1.25, out var zoomMin, out var zoomMax))
                    continue;

                var uiView = uiDocument.GetOpenUIViews()
                    .FirstOrDefault(openView => openView.ViewId == view.Id);
                uiView?.ZoomAndCenterRectangle(zoomMin, zoomMax);
            }
            catch
            {
                // Zoom failures stay silent.
            }
        }
    }

    // --- section box (separate entry; not part of click select) ---

    private static void ApplySectionBox(UIDocument uiDocument, IReadOnlyList<SearchMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(uiDocument);
        ArgumentNullException.ThrowIfNull(matches);

        // Section box is View3D-only
        if (matches.Count == 0 || uiDocument.ActiveView is not View3D view)
            return;

        XYZ? unionMin = null;
        XYZ? unionMax = null;

        foreach (var match in matches)
        {
            // Exact boxes; Expand once after the final union.
            if (!ElementBox.TryMatchBox(uiDocument.Document, match, view, factor: 1.0, out var min, out var max))
                continue;
            ElementBox.Union(ref unionMin, ref unionMax, min, max);
        }

        if (unionMin is null || unionMax is null)
            return;

        ElementBox.Expand(unionMin, unionMax, 1.25, out var expandedMin, out var expandedMax);

        using var tx = new Transaction(uiDocument.Document, "Section Box");
        tx.Start();
        view.SetSectionBox(new BoundingBoxXYZ { Min = expandedMin, Max = expandedMax });
        tx.Commit();
    }

    // --- host helpers ---

    private static bool HasSameOwnerView(Document document, ICollection<ElementId> elementIds)
    {
        ElementId? ownerViewId = null;
        foreach (var id in elementIds)
        {
            var viewId = document.GetElement(id)?.OwnerViewId ?? ElementId.InvalidElementId;
            if (viewId == ElementId.InvalidElementId)
                return false;
            ownerViewId ??= viewId;
            if (viewId != ownerViewId)
                return false;
        }

        return true;
    }

    private static ICollection<ElementId> ResolveVisibleIds(Document document, ICollection<ElementId> elementIds)
    {
        List<ElementId>? expanded = null;
        foreach (var id in elementIds)
        {
            if (document.GetElement(id) is not IndependentTag tag)
                continue;

            expanded ??= [..elementIds];
            foreach (var hostId in tag.GetTaggedLocalElementIds())
            {
                if (hostId != ElementId.InvalidElementId)
                    expanded.Add(hostId);
            }
        }

        return expanded ?? elementIds;
    }
}
