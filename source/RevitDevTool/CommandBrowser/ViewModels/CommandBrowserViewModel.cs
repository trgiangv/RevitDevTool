using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using DevTools.UI.Theme;
using RevitDevTool.CommandBrowser.Models;
using RevitDevTool.CommandBrowser.Services;

namespace RevitDevTool.CommandBrowser.ViewModels;

/// <summary>
/// ViewModel for the Command Browser bar.
/// Builds a composite list where recent items appear at the top AND remain in All Items.
/// Autocomplete filtering is handled by the AutoCompleteComboBox control.
/// </summary>
public sealed partial class CommandBrowserViewModel : ObservableObject, IDisposable
{
    private readonly CommandBrowserCache _cache;
    private readonly RibbonSnoopService _snoopService;
    private readonly ObservableCollection<GroupedCommandEntry> _entries = [];

    [ObservableProperty]
    public partial SolidColorBrush IconsBackground { get; private set; } = new(Colors.Transparent);

    [ObservableProperty]
    public partial bool ShowOnlyFavorites { get; set; }

    partial void OnShowOnlyFavoritesChanged(bool value)
    {
        AllItemsView?.Refresh();
    }

    [ObservableProperty]
    public partial ICollectionView? AllItemsView { get; private set; }

    public CommandBrowserViewModel(RibbonSnoopService snoopService, CommandBrowserCache cache)
    {
        _snoopService = snoopService;
        _cache = cache;

        snoopService.CommandExecuted += OnRibbonCommandExecuted;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;
        RefreshIconsBackground();
    }

    public void InitializeView()
    {
        if (AllItemsView is not null) return;
        RebuildEntries();
        BuildView();
    }

    [RelayCommand]
    private void Run(BrowserCommandItem? command)
    {
        if (command is null) return;
        _cache.AddRecent(command);
        _cache.Save();
        RebuildEntries();
        command.Run();
    }

    [RelayCommand]
    private void ToggleFavorite(BrowserCommandItem? command)
    {
        if (command is null) return;
        _cache.ToggleFavorite(command);
        _cache.Save();
        AllItemsView?.Refresh();
    }

    private void OnRibbonCommandExecuted(BrowserCommandItem command)
    {
        _cache.AddRecent(command);
        _cache.Save();
        RebuildEntries();
    }

    /// <summary>
    /// Rebuilds the composite list: Recent entries first, then all commands in AllItems.
    /// The same <see cref="BrowserCommandItem"/> can appear in both groups.
    /// </summary>
    private void RebuildEntries()
    {
        _entries.Clear();

        foreach (var cmd in _snoopService.AllCommands)
        {
            if (_cache.IsRecent(cmd.RibbonInfo.Id))
                _entries.Add(new GroupedCommandEntry(cmd, ItemGroup.Recent));
        }

        foreach (var cmd in _snoopService.AllCommands)
            _entries.Add(new GroupedCommandEntry(cmd, ItemGroup.AllItems));

        AllItemsView?.Refresh();
    }

    private void BuildView()
    {
        var view = CollectionViewSource.GetDefaultView(_entries);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        view.Filter = GroupFilter;
        AllItemsView = view;
    }

    private bool GroupFilter(object obj)
    {
        if (obj is not GroupedCommandEntry entry) return false;
        if (entry.Group == ItemGroup.Recent) return true;
        return !ShowOnlyFavorites || entry.Command.IsFavorite;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        RefreshIconsBackground();
        foreach (var cmd in _snoopService.AllCommands)
            cmd.RibbonInfo.RefreshImage();
    }

    private void RefreshIconsBackground()
    {
        var isDark = ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark;
        IconsBackground = new SolidColorBrush(isDark
            ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B4552")
            : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F4F4F4"));
    }

    public void Dispose()
    {
        _snoopService.CommandExecuted -= OnRibbonCommandExecuted;
        ThemeManager.Current.ActualApplicationThemeChanged -= OnThemeChanged;
    }
}
