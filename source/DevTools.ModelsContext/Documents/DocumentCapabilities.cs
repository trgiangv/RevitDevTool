namespace DevTools.ModelsContext.Documents;

[Flags]
public enum DocumentCapabilities
{
    None = 0,
    Elements = 1,
    Categories = 2,
    LinkInstance = 4,
    LinkElements = 8,
    LinkShow = 16,
    LinkFilters = 32
}
