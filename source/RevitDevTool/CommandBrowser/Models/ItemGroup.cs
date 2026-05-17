namespace RevitDevTool.CommandBrowser.Models;

public sealed class ItemGroup(string name, int order)
{
    [UsedImplicitly] public string Name { get; } = name;
    [UsedImplicitly] public int Order { get; } = order;

    public static readonly ItemGroup Recent = new("Recent", 0);
    public static readonly ItemGroup AllItems = new("All Items", 1);

    public override string ToString() => Name;
}

/// <summary>
/// Thin wrapper pairing a <see cref="BrowserCommandItem"/> with a display group.
/// Allows the same command to appear in both Recent and AllItems simultaneously.
/// </summary>
public sealed class GroupedCommandEntry(BrowserCommandItem command, ItemGroup group)
{
    public BrowserCommandItem Command { get; } = command;
    public ItemGroup Group { get; } = group;
}
