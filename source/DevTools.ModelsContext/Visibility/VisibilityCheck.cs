namespace DevTools.ModelsContext.Visibility;

public readonly record struct VisibilityCheck(string? DisabledReason, bool UsesReferenceCommands, bool UsesLinkFilter = false);
