namespace DevTools.ModelsContext.Model;

public sealed record ViewInfo(DocumentKey Model, string ViewId, string Name, bool Is3D);

public sealed record TemporaryState(ViewInfo View, bool HideIsolateActive, bool ViewPropertiesActive)
{
    public bool IsActive => HideIsolateActive || ViewPropertiesActive;
}
