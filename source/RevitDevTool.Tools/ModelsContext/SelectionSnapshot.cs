namespace RevitDevTool.Tools.ModelsContext;

internal sealed class SelectionSnapshot
{
    private readonly ICollection<ElementId> _ids;
    private readonly string _modelId;
    private readonly IList<Reference> _references;

    private SelectionSnapshot(string modelId, IList<Reference> references, ICollection<ElementId> ids)
    {
        _modelId = modelId;
        _references = references;
        _ids = ids;
    }

    public static SelectionSnapshot Take(UIDocument active)
    {
        return new SelectionSnapshot(DocumentRegistry.ModelIdOf(active.Document),
            active.Selection.GetReferences(), active.Selection.GetElementIds());
    }

    public void Restore(UIApplication application)
    {
        if (application.ActiveUIDocument is not { } active
            || !string.Equals(DocumentRegistry.ModelIdOf(active.Document), _modelId, StringComparison.Ordinal))
        {
            return;
        }

        if (!_references.Any(IsLinked))
        {
            active.Selection.SetElementIds(_ids);
            return;
        }

        active.Selection.SetReferences(Combined(active.Document));
    }

    private List<Reference> Combined(Document model)
    {
        var covered = new HashSet<ElementId>(_references.Select(reference => reference.ElementId));
        var combined = new List<Reference>(_references);
        combined.AddRange(_ids.Where(id => !covered.Contains(id))
            .Select(model.GetElement)
            .Where(element => element is not null)
            .Select(element => new Reference(element)));
        return combined;
    }

    private static bool IsLinked(Reference reference)
    {
        return reference.LinkedElementId != ElementId.InvalidElementId;
    }
}
