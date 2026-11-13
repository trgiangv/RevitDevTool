namespace DevTools.ModelsContext.Model;

public readonly record struct SourceId(string Value)
{
    public override string ToString()
    {
        return Value;
    }
}
