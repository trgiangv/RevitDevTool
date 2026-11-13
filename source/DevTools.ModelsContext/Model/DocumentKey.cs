namespace DevTools.ModelsContext.Model;

public readonly record struct DocumentKey(string ModelId, long LinkInstanceId)
{
    public const long NoLink = -1;

    public bool IsCurrentModel => LinkInstanceId == NoLink;

    public static DocumentKey Current(string modelId)
    {
        return new DocumentKey(modelId, NoLink);
    }

    public static DocumentKey Link(string modelId, long linkInstanceId)
    {
        if (linkInstanceId == NoLink)
        {
            throw new ArgumentOutOfRangeException(nameof(linkInstanceId));
        }

        return new DocumentKey(modelId, linkInstanceId);
    }

    public DocumentKey CurrentModel => Current(ModelId);

    public override string ToString()
    {
        return IsCurrentModel ? ModelId : $"{ModelId}@{LinkInstanceId}";
    }
}
