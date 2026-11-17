using Bogus;
using DevTools.ModelsContext.Engine;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeTypeBound
{
    public const long NoType = -1;

    private static readonly string?[] Ratings = [null, "", "1 HR", "2 HR", "NR"];

    private readonly Dictionary<long, string> _instanceValues;
    private readonly Dictionary<long, string?> _typeValues;

    private FakeTypeBound(
        long[] elements,
        long[] typeIds,
        Dictionary<long, string?> typeValues,
        Dictionary<long, string> instanceValues)
    {
        Elements = elements;
        TypeIds = typeIds;
        _typeValues = typeValues;
        _instanceValues = instanceValues;
    }

    public long[] Elements { get; }

    public long[] TypeIds { get; }

    public TypePresence Presence => new(
        _typeValues.Where(pair => pair.Value is { Length: > 0 }).Select(pair => pair.Key).ToList(),
        _typeValues.Where(pair => pair.Value is "").Select(pair => pair.Key).ToList());

    public TypeMembership Members => new(Elements, TypeIds);

    public static FakeTypeBound Create(int seed, int elements = 400, int types = 24)
    {
        var faker = new Faker { Random = new Randomizer(seed) };
        var typeValues = Enumerable.Range(1, types).ToDictionary(index => (long)index, _ => faker.PickRandom(Ratings));
        var ids = Enumerable.Range(1000, elements).Select(id => (long)id).ToArray();
        var typeIds = ids.Select(_ => faker.Random.Bool(0.05f) ? NoType : faker.Random.Long(1, types)).ToArray();
        var phantoms = ids.Where(_ => faker.Random.Bool(0.15f)).ToDictionary(id => id, _ => string.Empty);
        return new FakeTypeBound(ids, typeIds, typeValues, phantoms);
    }

    public string? ValueOf(int index)
    {
        var own = _instanceValues.TryGetValue(Elements[index], out var value) ? value : null;
        if (own is { Length: > 0 })
        {
            return own;
        }

        return _typeValues.TryGetValue(TypeIds[index], out var typed) && typed is not null ? typed : own;
    }

    public bool HasValueRule(int index)
    {
        return _instanceValues.TryGetValue(Elements[index], out var value) && value.Length > 0;
    }

    public bool HasNoValueRule(int index)
    {
        return _instanceValues.TryGetValue(Elements[index], out var value) && value.Length == 0;
    }

    public bool NativeEquals(int index, string value)
    {
        return string.Equals(ValueOf(index) ?? string.Empty, value, StringComparison.OrdinalIgnoreCase);
    }

    public bool NativeNotEquals(int index, string value)
    {
        return !NativeEquals(index, value);
    }
}
