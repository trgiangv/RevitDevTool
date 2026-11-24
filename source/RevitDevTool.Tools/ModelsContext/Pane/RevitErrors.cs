namespace RevitDevTool.Tools.ModelsContext.Pane;

internal static class RevitErrors
{
    public const string Busy = "Revit is busy.";
    public const string NotOpen = "This model is not open.";

    public static void EnsureEditable(Document document)
    {
        if (document.IsModifiable || document.IsReadOnly)
        {
            throw new InvalidOperationException(Busy);
        }
    }
}
