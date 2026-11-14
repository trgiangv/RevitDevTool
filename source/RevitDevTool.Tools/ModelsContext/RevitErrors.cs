namespace RevitDevTool.Tools.ModelsContext;

internal static class RevitErrors
{
    public const string Busy = "Revit is busy.";
    public const string NotOpen = "This model is not open.";
    public const string NoActiveView = "There is no active view.";

    public static void EnsureEditable(Document document)
    {
        if (document.IsModifiable || document.IsReadOnly)
        {
            throw new InvalidOperationException(Busy);
        }
    }
}
