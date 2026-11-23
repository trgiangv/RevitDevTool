using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Explorer;

internal sealed class ExplorerSelection
{
    public HashSet<TreeRow> Rows { get; } = [];

    public HashSet<ElementRef> Elements { get; set; } = [];

    public int Version { get; private set; }

    public int NextVersion()
    {
        return ++Version;
    }

    public void ReplaceRows(IEnumerable<TreeRow> rows)
    {
        Rows.Clear();
        Rows.UnionWith(rows);
    }
}
