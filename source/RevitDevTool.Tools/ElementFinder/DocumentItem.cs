namespace RevitDevTool.Tools.ElementFinder;

public sealed class DocumentItem(Document document, RevitLinkInstance? linkInstance = null)
{
    public Document Document { get; } = document;
    public RevitLinkInstance? LinkInstance { get; } = linkInstance;
    public string Title { get; } = document.Title;
    public bool IsLinked => LinkInstance is not null;
}
