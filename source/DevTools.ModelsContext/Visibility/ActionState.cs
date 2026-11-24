namespace DevTools.ModelsContext.Visibility;

public readonly record struct ActionState(string? DisabledReason, string? Note = null)
{
    public static string? NoteOf(bool usesLinkFilter, bool usesLinkHide)
    {
        return usesLinkFilter ? VisibilityText.LinkFilterNote
            : usesLinkHide ? VisibilityText.SavedWithView
            : null;
    }
}
