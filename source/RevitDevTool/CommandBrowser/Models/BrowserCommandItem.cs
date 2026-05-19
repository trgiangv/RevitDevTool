using RevitDevTool.Core;

namespace RevitDevTool.CommandBrowser.Models;

/// <summary>
/// Represents a single runnable Revit command discovered from the ribbon.
/// Combines the ribbon metadata with a <see cref="RevitCommandId"/> for execution.
/// </summary>
public sealed partial class BrowserCommandItem(RibbonCommandInfo ribbonInfo, RevitCommandId commandId) : ObservableObject, IEquatable<BrowserCommandItem>
{
    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public RibbonCommandInfo RibbonInfo { get; } = ribbonInfo;
    private RevitCommandId CommandId { get; } = commandId;

    public void Run()
    {
        RevitContextExecutor.Raise(uiApp => uiApp.PostCommand(CommandId));
    }

    public bool Equals(BrowserCommandItem? other) =>
        other is not null && CommandId.Id == other.CommandId.Id;

    public override bool Equals(object? obj) =>
        obj is BrowserCommandItem other && Equals(other);

    public override int GetHashCode() => (int)CommandId.Id;

    public override string ToString() => $"{CommandId.Id}: {RibbonInfo.Id}";
}
