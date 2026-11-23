namespace DevTools.ModelsContext.Documents;

public sealed record ElementDelta(
    SourceId Source,
    IReadOnlyList<TypeRecord> Types,
    IReadOnlyList<ElementRecord> Added,
    IReadOnlyList<ElementRecord> Modified,
    IReadOnlyList<long> Deleted,
    bool IsUndoRedo = false)
{
    public int ChangedCount => Types.Count + Added.Count + Modified.Count + Deleted.Count;

    public static ElementDelta DeletedOnly(SourceId source, IReadOnlyList<long> deleted)
    {
        return new ElementDelta(source, [], [], [], deleted);
    }
}
