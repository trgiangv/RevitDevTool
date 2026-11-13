namespace DevTools.ModelsContext.Model;

public sealed record DocumentInfo(
    DocumentKey Key,
    SourceId Source,
    string DisplayName,
    bool IsLoaded,
    DocumentCapabilities Capabilities)
{
    public bool Can(DocumentCapabilities capability)
    {
        return (Capabilities & capability) == capability;
    }
}
