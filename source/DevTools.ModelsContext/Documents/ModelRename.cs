namespace DevTools.ModelsContext.Documents;

public sealed record ModelRename(string OldId, string NewId, SourceId OldSource, SourceId NewSource)
{
    public DocumentKey Map(DocumentKey key)
    {
        return key.ModelId == OldId ? key with { ModelId = NewId } : key;
    }

    public ElementRef Map(ElementRef element)
    {
        return element with { Document = Map(element.Document) };
    }

    public SourceId Map(SourceId source)
    {
        return source == OldSource ? NewSource : source;
    }

    public DocumentInfo Map(DocumentInfo info)
    {
        return info with { Key = Map(info.Key), Source = Map(info.Source) };
    }

    public ViewInfo Map(ViewInfo view)
    {
        return view with { Model = Map(view.Model) };
    }
}
