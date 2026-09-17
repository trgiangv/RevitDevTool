namespace RevitDevTool.Logging.Linkify;

internal static class ElementSelector
{
    public static void Select(UIDocument uiDocument, SearchMatch match)
    {
        ArgumentNullException.ThrowIfNull(uiDocument);
        ArgumentNullException.ThrowIfNull(match);

        try
        {
            switch (match)
            {
                case SearchMatch.HostElements host:
                    SelectHost(uiDocument, host.Ids);
                    break;
                case SearchMatch.LinkedElements linked:
                    SelectLinked(uiDocument, linked.Instance, linked.Ids);
                    break;
            }
        }
        catch
        {
            // Monitor click is silent on failure.
        }
    }

    private static void SelectHost(UIDocument uiDocument, ICollection<ElementId> elementIds)
    {
        if (elementIds.Count == 0)
            return;

        var document = uiDocument.Document;
        var first = document.GetElement(elementIds.First());
        switch (first)
        {
            case null:
                return;
            case Autodesk.Revit.DB.View view:
                uiDocument.RequestViewChange(view);
                return;
        }

        if (first.OwnerViewId != ElementId.InvalidElementId && HasSameOwnerView(document, elementIds))
        {
            var ownerView = (Autodesk.Revit.DB.View)document.GetElement(first.OwnerViewId);
            uiDocument.RequestViewChange(ownerView);
        }

        uiDocument.Selection.SetElementIds(elementIds);
        uiDocument.ShowElements(ResolveVisibleIds(document, elementIds));

        var linkedTagged = CollectLinkedTagged(document, elementIds);
        if (linkedTagged is null)
            return;

        foreach (var linked in linkedTagged)
            SelectLinked(uiDocument, linked.Instance, linked.Ids);
    }

    private static void SelectLinked(
        UIDocument uiDocument,
        RevitLinkInstance instance,
        ICollection<ElementId> elementIds)
    {
        if (elementIds.Count == 0)
            return;

        var linkDoc = instance.GetLinkDocument();
        if (linkDoc is null)
            return;

        var elements = new List<Element>(elementIds.Count);
        foreach (var id in elementIds)
        {
            var element = linkDoc.GetElement(id);
            if (element is not null)
                elements.Add(element);
        }

        if (elements.Count == 0)
            return;

        try
        {
#if REVIT2023_OR_GREATER
            var references = new List<Reference>(elements.Count);
            foreach (var element in elements)
                references.Add(new Reference(element).CreateLinkReference(instance));
            uiDocument.Selection.SetReferences(references);
#else
            uiDocument.Selection.SetElementIds([instance.Id]);
#endif
        }
        catch
        {
            // Selection may fail (year/API/view); still zoom.
        }

        ZoomLinked(uiDocument, instance, elements);
    }

    private static void ZoomLinked(
        UIDocument uiDocument,
        RevitLinkInstance instance,
        IReadOnlyList<Element> elements)
    {
        try
        {
            var activeView = uiDocument.ActiveView;
            if (activeView is null)
                return;
            if (!LinkBoundingBox.TryGetBoundingBox(elements, instance, activeView, 1.25, out var hostMin, out var hostMax))
                return;

            UIView? uiView = null;
            foreach (var openView in uiDocument.GetOpenUIViews())
            {
                if (openView.ViewId != activeView.Id)
                    continue;
                uiView = openView;
                break;
            }

            uiView?.ZoomAndCenterRectangle(hostMin, hostMax);
        }
        catch
        {
            // Zoom failures (sheet/drafting views, empty box) stay silent.
        }
    }

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

    private static List<SearchMatch.LinkedElements>? CollectLinkedTagged(Document document, ICollection<ElementId> elementIds)
    {
        Dictionary<ElementId, (RevitLinkInstance Instance, List<ElementId> Ids)>? byInstance = null;
        foreach (var id in elementIds)
        {
            if (document.GetElement(id) is not IndependentTag tag)
                continue;

            foreach (var linkElementId in tag.GetTaggedElementIds())
            {
                if (linkElementId.LinkInstanceId == ElementId.InvalidElementId)
                    continue;

                if (document.GetElement(linkElementId.LinkInstanceId) is not RevitLinkInstance instance)
                    continue;

                var linkDoc = instance.GetLinkDocument();
                if (linkDoc?.GetElement(linkElementId.LinkedElementId) is null)
                    continue;

                byInstance ??= [];
                if (!byInstance.TryGetValue(instance.Id, out var group))
                {
                    group = (instance, []);
                    byInstance[instance.Id] = group;
                }

                group.Ids.Add(linkElementId.LinkedElementId);
            }
        }

        if (byInstance is null)
            return null;

        var matches = new List<SearchMatch.LinkedElements>(byInstance.Count);
        foreach (var group in byInstance.Values)
            matches.Add(new SearchMatch.LinkedElements(group.Instance, group.Ids));
        return matches;
    }
}
