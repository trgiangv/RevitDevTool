namespace DevTools.ModelsContext.Model;

public readonly record struct ElementRef(DocumentKey Document, long Id)
{
    public override string ToString()
    {
        return Document.IsCurrentModel ? Id.ToString() : $"{Document.LinkInstanceId}@{Id}";
    }
}
