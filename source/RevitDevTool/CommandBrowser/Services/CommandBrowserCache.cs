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
    private readonly List<string> _recentIds = [];
    private readonly HashSet<string> _favoriteIds = [];

    public bool IsBarVisible { get; set; } = true;

    public bool IsRecent(string commandId) => _recentIds.Contains(commandId);

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
    /// Evicts the oldest entry when capacity is reached. Session-only, not persisted.
    /// </summary>
    public void AddRecent(BrowserCommandItem command)
    {
        _recentIds.Remove(command.RibbonInfo.Id);

        if (_recentIds.Count >= MaxRecentCommands)
            _recentIds.RemoveAt(_recentIds.Count - 1);

        _recentIds.Insert(0, command.RibbonInfo.Id);
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
