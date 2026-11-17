namespace DevTools.ModelsContext.Model;

public readonly record struct NumberRange(double Min, double Max)
{
    public double Middle => (Min + Max) / 2;

    public double HalfSpan => (Max - Min) / 2;

    public static NumberRange Of(double value)
    {
        return new NumberRange(value, value);
    }

    public NumberRange With(double value)
    {
        return new NumberRange(Math.Min(Min, value), Math.Max(Max, value));
    }

    public NumberRange Union(NumberRange other)
    {
        return new NumberRange(Math.Min(Min, other.Min), Math.Max(Max, other.Max));
    }
}

public sealed record ParameterValueCounts(
    int Total,
    IReadOnlyDictionary<string, int> Values,
    IReadOnlyDictionary<string, NumberRange>? Numbers = null);

public readonly record struct ValueCount(string Value, int Count, NumberRange? Stored = null)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed record ValueIndex(IReadOnlyList<ValueCount> Values, bool HasMore, int NoParameterCount);
