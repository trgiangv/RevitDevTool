namespace DevTools.ModelsContext.Documents;

public sealed record ViewInfo(DocumentKey Model, string ViewId, string Name, ViewKind Kind)
{
    public bool Is3D => Kind == ViewKind.ThreeD;
}
