namespace DevTools.ModelsContext.Model;

public sealed class TreeSchema
{
    public const int MaxGroupingDepth = 3;

    public TreeSchema(IReadOnlyList<string> levels)
    {
        if (levels.Count is < 2 or > MaxGroupingDepth + 1)
        {
            throw new ArgumentException("A tree schema has a group level, up to two more levels and an instance level.",
                nameof(levels));
        }

        Levels = levels;
    }

    public IReadOnlyList<string> Levels { get; }

    public int GroupingDepth => Levels.Count - 1;

    public string GroupLevel => Levels[0];

    public string InstanceLevel => Levels[^1];
}
