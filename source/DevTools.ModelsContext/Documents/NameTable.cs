namespace DevTools.ModelsContext.Documents;

public sealed class NameTable
{
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private readonly List<string> _names = [];

    public int Count => _names.Count;

    public IReadOnlyList<string> Names => _names;

    public string this[int index] => _names[index];

    public int Intern(string name)
    {
        if (_index.TryGetValue(name, out var index))
        {
            return index;
        }

        index = _names.Count;
        _names.Add(name);
        _index.Add(name, index);
        return index;
    }
}
