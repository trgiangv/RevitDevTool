namespace DevTools.ModelsContext.Documents;

public sealed class DocumentSnapshotBuilder(SourceId source, int groupingDepth = DocumentSnapshot.MaxGroupingDepth)
{
    private readonly DocumentSnapshot _snapshot = new(source, groupingDepth);
    private bool _built;

    public int ElementCount => _snapshot.ElementCount;

    public DocumentSnapshotBuilder AddType(TypeRecord type)
    {
        EnsureOpen();
        _snapshot.UpsertType(type, out _);
        return this;
    }

    public DocumentSnapshotBuilder AddElement(ElementRecord element)
    {
        EnsureOpen();
        if (!_snapshot.TryAdd(element))
        {
            throw new ArgumentException($"Element {element.Id} is a duplicate or has an unknown type {element.TypeId}.",
                nameof(element));
        }

        return this;
    }

    public DocumentSnapshotBuilder WithStamp(DocumentVersion stamp)
    {
        EnsureOpen();
        _snapshot.SetStamp(stamp);
        return this;
    }

    public DocumentSnapshot Build()
    {
        EnsureOpen();
        _built = true;
        _snapshot.TrimExcess();
        return _snapshot;
    }

    private void EnsureOpen()
    {
        if (_built)
        {
            throw new InvalidOperationException("The snapshot is already built.");
        }
    }
}
