using DevTools.Settings;
using RevitDevTool.CommandBrowser.Models;

namespace RevitDevTool.CommandBrowser.Services;

/// <summary>
/// Manages favorites (persisted) and recent commands (session-only in-memory).
/// Recent commands follow MRU ordering with a fixed capacity and reset each Revit session.
/// </summary>
public sealed class CommandBrowserCache(IFileConfig<PathOptions> fileConfig)
{
    private const int MaxRecentCommands = 10;
    private readonly LinkedList<string> _recentOrder = [];
    private readonly HashSet<string> _recentSet = new(StringComparer.Ordinal);
    private readonly HashSet<string> _favoriteIds = new(StringComparer.Ordinal);

    public bool IsBarVisible { get; set; } = true;

    public bool IsRecent(string commandId) => _recentSet.Contains(commandId);

    public bool IsFavorite(string commandId) => _favoriteIds.Contains(commandId);

    /// <summary>
    /// Restores favorites and bar visibility from persisted config.
    /// Recent list is intentionally NOT restored -- it is session-only.
    /// </summary>
    public void Load(IReadOnlyCollection<BrowserCommandItem> allCommands)
    {
        var config = fileConfig.Load<CommandBrowserConfig>() ?? new CommandBrowserConfig();

        IsBarVisible = config.IsBarVisible;

        _favoriteIds.Clear();
        foreach (var id in config.FavoriteCommandIds)
            _favoriteIds.Add(id);

        foreach (var cmd in allCommands)
            cmd.IsFavorite = _favoriteIds.Contains(cmd.RibbonInfo.Id);
    }

    public void Save()
    {
        var config = new CommandBrowserConfig
        {
            FavoriteCommandIds = [.._favoriteIds],
            IsBarVisible = IsBarVisible
        };
        fileConfig.Save(config);
    }

    /// <summary>
    /// Promotes a command to the top of the recent list (MRU order).
    /// Uses LinkedList + HashSet for O(1) contains, O(1) add/remove.
    /// </summary>
    public void AddRecent(BrowserCommandItem command)
    {
        var id = command.RibbonInfo.Id;

        if (_recentSet.Contains(id))
        {
            _recentOrder.Remove(id);
        }
        else if (_recentOrder.Count >= MaxRecentCommands)
        {
            var last = _recentOrder.Last!.Value;
            _recentOrder.RemoveLast();
            _recentSet.Remove(last);
        }

        _recentOrder.AddFirst(id);
        _recentSet.Add(id);
    }

    public void ToggleFavorite(BrowserCommandItem command)
    {
        var id = command.RibbonInfo.Id;
        if (_favoriteIds.Remove(id))
        {
            command.IsFavorite = false;
        }
        else
        {
            _favoriteIds.Add(id);
            command.IsFavorite = true;
        }
    }
}
