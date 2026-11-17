namespace DevTools.ModelsContext.Model;

public readonly record struct SourceId(string Value)
{
    public string Label
    {
        get
        {
            var start = Math.Max(Value.LastIndexOfAny(['\\', '/']), Value.LastIndexOf(':')) + 1;
            return start < Value.Length ? Value.Substring(start) : Value;
        }
    }

    public override string ToString()
    {
        return Value;
    }
}
