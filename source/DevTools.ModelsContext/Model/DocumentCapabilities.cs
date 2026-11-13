namespace DevTools.ModelsContext.Model;

[Flags]
public enum DocumentCapabilities
{
    None = 0,
    HideById = 1,
    HideGroupEverywhere = 2,
    WholeModel = 4,
    ReferenceCommands = 8
}
