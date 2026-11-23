using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DevTools.ModelsContext.Explorer;

internal sealed class ExplorerSearch(PanelRuntime runtime, ExplorerTree tree, TreeReveal reveal)
{
    private static readonly Regex IdToken = new(@"^(?:(\d+)@)?(\d+)$", RegexOptions.CultureInvariant);

    private HashSet<TreeRow>? _hits;
    private HashSet<TreeRow> _matches = [];

    public void Clear()
    {
        _hits = null;
        _matches = [];
    }

    public List<TreeRow> Visible()
    {
        var visible = new List<TreeRow>();
        Flatten(tree.Roots, false, visible);
        return visible;
    }

    public List<TreeRow> VisibleChildren(TreeRow row)
    {
        var visible = new List<TreeRow>();
        Flatten(row.Children!, IsUnderMatch(row), visible);
        return visible;
    }

    public async Task CollectAsync(string text)
    {
        if (text.Length == 0)
        {
            Clear();
            return;
        }

        var hits = new HashSet<TreeRow>();
        var matches = new HashSet<TreeRow>();
        foreach (var root in tree.Roots)
        {
            await CollectHitsAsync(root, text, hits, matches);
        }

        foreach (var element in ParseToken(text))
        {
            for (var row = await reveal.RevealAsync(element); row is not null; row = row.Parent)
            {
                matches.Add(row);
                hits.Add(row);
            }
        }

        _hits = hits;
        _matches = matches;
    }

    private static bool Matches(TreeRow row, string text)
    {
        return row.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void Flatten(IEnumerable<TreeRow> rows, bool underMatch, List<TreeRow> into)
    {
        foreach (var row in rows)
        {
            if (_hits is not null && !underMatch && !_hits.Contains(row))
            {
                continue;
            }

            if (row.IsDimmed && _hits?.Contains(row) != true && !tree.ShowsHidden(row.Parent))
            {
                continue;
            }

            into.Add(row);
            if (row.IsExpanded && row.Children is { } children)
            {
                Flatten(children, underMatch || _matches.Contains(row), into);
            }
        }
    }

    private bool IsUnderMatch(TreeRow row)
    {
        for (var current = row; current is not null; current = current.Parent)
        {
            if (_matches.Contains(current))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> CollectHitsAsync(TreeRow row, string text, HashSet<TreeRow> hits, HashSet<TreeRow> matches)
    {
        var self = Matches(row, text);
        if (self)
        {
            matches.Add(row);
        }

        if (SearchesInside(row))
        {
            await tree.EnsureChildrenAsync(row);
        }

        var any = await CollectChildHitsAsync(row, text, hits, matches);
        if (any)
        {
            row.IsExpanded = true;
        }

        if (self || any)
        {
            hits.Add(row);
        }

        return self || any;
    }

    private async Task<bool> CollectChildHitsAsync(TreeRow row, string text, HashSet<TreeRow> hits,
        HashSet<TreeRow> matches)
    {
        var any = false;
        foreach (var child in row.Children ?? [])
        {
            any |= await CollectHitsAsync(child, text, hits, matches);
        }

        return any;
    }

    private bool SearchesInside(TreeRow row)
    {
        return row.Kind is TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel or TreeRowKind.Family
               || (row.Kind == TreeRowKind.Group && (tree.DocumentOf(row)?.Snapshot.GroupingDepth ?? 0) > 1);
    }

    private IEnumerable<ElementRef> ParseToken(string text)
    {
        var match = IdToken.Match(text);
        if (!match.Success || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out var id))
        {
            return [];
        }

        long? link = match.Groups[1].Success
            ? long.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture)
            : null;
        return runtime.Data.Documents
            .Where(document => link is null || document.Key.LinkInstanceId == link)
            .Where(document => document.Snapshot.Contains(id))
            .Select(document => new ElementRef(document.Key, id))
            .ToList();
    }
}
