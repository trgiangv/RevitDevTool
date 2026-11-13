namespace DevTools.ModelsContext.Model;

public sealed record ParameterValueCounts(int Total, IReadOnlyDictionary<string, int> Values);

public readonly record struct ValueCount(string Value, int Count);

public sealed record ValueIndex(IReadOnlyList<ValueCount> Values, bool HasMore, int NoParameterCount);
