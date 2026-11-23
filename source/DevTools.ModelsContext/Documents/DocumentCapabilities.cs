namespace DevTools.ModelsContext.Documents;

[Flags]
public enum DocumentCapabilities
{
    None = 0,
    HideById = 1,
    HideGroupEverywhere = 2,
    WholeModel = 4,
    ReferenceCommands = 8,
    ReferenceUnhide = 16,
    LinkFilters = 32
}
