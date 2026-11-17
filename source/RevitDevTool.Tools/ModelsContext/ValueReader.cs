using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class ValueReader(ParameterKey parameter, CandidateSet candidates)
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NumberRange> _numbers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<long>> _raw = new(StringComparer.Ordinal);

    public int AddSlice(Document document, int start)
    {
        var ids = candidates.Ids;
        var watch = Stopwatch.StartNew();
        var position = start;
        while (position < ids.Length && position - start < ElementReader.SliceSize
                                     && watch.Elapsed < ElementReader.SliceTime)
        {
            if (document.GetElement(ids[position].ToElementId()) is { } element)
            {
                Add(element, ids[position]);
            }

            position++;
        }

        return position;
    }

    public ValueEntry ToEntry()
    {
        return new ValueEntry(candidates.ModelIds.Length, _counts,
            _raw.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal), _numbers);
    }

    public void Add(Element element, long id)
    {
        if (ParameterLocator.Find(element, parameter) is not { } value
            || ParameterValues.ValueText(element.Document, value) is not { } text)
        {
            return;
        }

        if (candidates.InModel(id))
        {
            _counts[text] = _counts.TryGetValue(text, out var count) ? count + 1 : 1;
        }

        if (value.StorageType == StorageType.Double)
        {
            var number = value.AsDouble();
            _numbers[text] = _numbers.TryGetValue(text, out var range) ? range.With(number) : NumberRange.Of(number);
            return;
        }

        if (RawOf(value) is not { } raw)
        {
            return;
        }

        if (!_raw.TryGetValue(text, out var raws))
        {
            raws = [];
            _raw.Add(text, raws);
        }

        raws.Add(raw);
    }

    private static long? RawOf(Parameter value)
    {
        return value.StorageType switch
        {
            StorageType.Integer => value.AsInteger(),
            StorageType.ElementId => value.AsElementId().ToLong(),
            _ => null
        };
    }
}
