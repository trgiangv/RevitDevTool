using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class DocumentChangeReader(
    Document model,
    ICollection<ElementId> added,
    ICollection<ElementId> modified,
    ICollection<ElementId> deleted)
{
    public ChangeReport Read()
    {
        var bulk = ChangeClassifier.IsBulk(added.Count, modified.Count, deleted.Count);
        return new ChangeReport(
            added.Count,
            modified.Count,
            bulk ? [] : KindsOf(added),
            bulk ? [] : KindsOf(modified),
            deleted.Select(id => id.ToLong()).ToList());
    }

    public ElementDelta Delta(SourceId source, ChangeReport report, bool undoRedo)
    {
        if (report.IsBulk)
        {
            var dirty = new List<long>(report.Deleted);
            dirty.AddRange(added.Select(id => id.ToLong()));
            dirty.AddRange(modified.Select(id => id.ToLong()));
            return new ElementDelta(source, [], [], [], dirty, undoRedo);
        }

        var addedReader = ReadAll(added);
        var modifiedReader = ReadAll(OptionsChanged() ? WithOptionMembers() : modified);
        var types = addedReader.Types.Concat(modifiedReader.Types)
            .GroupBy(type => type.TypeId)
            .Select(group => group.Last())
            .ToList();
        return new ElementDelta(source, types, addedReader.Elements, modifiedReader.Elements, [.. report.Deleted],
            undoRedo);
    }

    public IReadOnlyCollection<long> Modified => modified.Select(id => id.ToLong()).ToList();

    public bool Keeps(CandidateSet candidates)
    {
        return candidates.Holds(model, modified);
    }

    public static TrackedIds TrackedOf(Document model)
    {
        return new TrackedIds(
            model.CollectElements().OfClass<RevitLinkInstance>().ToElementIds().Select(id => id.ToLong()),
            model.CollectElements().OfClass<ParameterElement>().ToElementIds().Select(id => id.ToLong()),
            model.CollectElements().OfClass<Family>().ToElementIds().Select(id => id.ToLong()));
    }

    public static void InvalidateLinks(Document model, ParameterCache cache)
    {
        foreach (var instance in model.CollectElements().OfClass<RevitLinkInstance>().Cast<RevitLinkInstance>())
        {
            cache.Invalidate(DocumentRegistry.LinkSourceOf(instance));
        }
    }

    public static List<LinkStatusChange> LinkStatusesOf(Document model, long linkTypeId, ParameterCache cache)
    {
        var typeId = linkTypeId.ToElementId();
        var statuses = new List<LinkStatusChange>();
        foreach (var instance in model.CollectElements()
                     .OfClass<RevitLinkInstance>()
                     .Cast<RevitLinkInstance>()
                     .Where(instance => instance.GetTypeId() == typeId))
        {
            cache.Invalidate(DocumentRegistry.LinkSourceOf(instance));
            statuses.Add(new LinkStatusChange(DocumentRegistry.KeyOf(model, instance),
                instance.GetLinkDocument() is null ? LinkStatus.Unloaded : LinkStatus.Reloaded));
        }

        return statuses;
    }

    private List<ChangedId> KindsOf(ICollection<ElementId> ids)
    {
        var kinds = new List<ChangedId>();
        foreach (var id in ids)
        {
            ChangedKind? kind = model.GetElement(id) switch
            {
                null => ChangedKind.Unknown,
                RevitLinkInstance => ChangedKind.LinkInstance,
                RevitLinkType => ChangedKind.LinkType,
                ParameterElement => ChangedKind.ParameterElement,
                Family => ChangedKind.Family,
                DesignOption => ChangedKind.DesignOption,
                ElementType => ChangedKind.ElementType,
                { Category: null } => ChangedKind.Unknown,
                _ => null
            };
            if (kind is { } known)
            {
                kinds.Add(new ChangedId(id.ToLong(), known));
            }
        }

        return kinds;
    }

    private bool OptionsChanged()
    {
        return added.Concat(modified).Any(id => model.GetElement(id) is DesignOption);
    }

    private HashSet<ElementId> WithOptionMembers()
    {
        var ids = new HashSet<ElementId>(modified);
        ids.UnionWith(ElementReader.OptionMemberIds(model).Select(id => id.ToElementId()));
        ids.ExceptWith(added);
        return ids;
    }

    private ElementReader ReadAll(ICollection<ElementId> ids)
    {
        var reader = new ElementReader(model, []);
        foreach (var id in ids)
        {
            if (model.GetElement(id) is { } element)
            {
                reader.Read(element);
            }
        }

        return reader;
    }
}
