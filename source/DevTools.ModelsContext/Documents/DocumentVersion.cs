namespace DevTools.ModelsContext.Documents;

public readonly record struct DocumentVersion(string VersionGuid, int Saves)
{
    public bool IsSameAs(DocumentVersion other)
    {
        return string.Equals(VersionGuid, other.VersionGuid, StringComparison.OrdinalIgnoreCase);
    }
}
