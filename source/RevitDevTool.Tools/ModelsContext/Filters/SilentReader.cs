using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;
using RevitDevTool.Tools.ModelsContext.Parameters;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal sealed class SilentReader(ParameterKey key, ResolvedParameter item, long[] unseen)
{
    private readonly List<long> _empty = [];
    private readonly List<long> _filled = [];
    private readonly HashSet<long> _typed = [.. item.Types.TypesFor(FilterOperator.Defined)];

    public int Count => unseen.Length;

    public static SilentReader For(Document document, ICollection<ElementId> categories, ParameterKey key, ResolvedParameter item)
    {
        try
        {
            return new SilentReader(key, item, ParameterPresence.UnseenIds(document, categories, item));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter silent", $"{document.Title} {key.Name}");
            return new SilentReader(key, item, []);
        }
    }

    public int AddSlice(Document document, int start)
    {
        var watch = Stopwatch.StartNew();
        var index = start;
        try
        {
            while (index < unseen.Length && watch.Elapsed < ElementReader.SliceTime)
            {
                if (document.GetElement(unseen[index].ToElementId()) is { } element)
                {
                    Read(element);
                }

                index++;
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter silent slice", $"{document.Title} {key.Name}");
            return unseen.Length;
        }

        return index;
    }

    public ResolvedParameter ToResolved()
    {
        var silent = _filled.Count + _empty.Count == 0 ? SilentElements.None : new SilentElements(_filled.ToArray(), _empty.ToArray());
        return item with { Silent = silent, SilentChecked = true };
    }

    private void Read(Element element)
    {
        var type = element.GetTypeId().ToLong();
        if (_typed.Contains(type))
        {
            return;
        }

        var id = item.ElementId;
        if (ParameterLocator.FindOn(element, key, id) is { } parameter && ParameterLocator.Lists(element, key, id))
        {
            (ParameterLocator.IsFilled(parameter) ? _filled : _empty).Add(element.Id.ToLong());
        }
    }
}
