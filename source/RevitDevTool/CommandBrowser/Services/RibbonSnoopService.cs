using System.Collections.Specialized;
using Autodesk.Internal.Windows;
using Autodesk.Windows;
using RevitDevTool.CommandBrowser.Models;
using RibbonItem = Autodesk.Windows.RibbonItem;
using RibbonPanel = Autodesk.Windows.RibbonPanel;

namespace RevitDevTool.CommandBrowser.Services;

/// <summary>
/// Discovers all postable commands by walking the Autodesk ribbon visual tree.
/// Subscribes to <see cref="ComponentManager.ItemExecuted"/> for auto-tracking recent usage
/// and to <see cref="INotifyCollectionChanged"/> on the ribbon tabs to detect late-loaded add-ins.
/// </summary>
public sealed class RibbonSnoopService : IDisposable
{
    private readonly Dictionary<string, BrowserCommandItem> _commandsById = new(StringComparer.Ordinal);
    private bool _disposed;

    public IReadOnlyCollection<BrowserCommandItem> AllCommands => _commandsById.Values;

    /// <summary>
    /// Raised when a ribbon command is executed by the user (from the ribbon, not from this browser).
    /// Carries the matching <see cref="BrowserCommandItem"/> so the cache can record it as recent.
    /// </summary>
    public event Action<BrowserCommandItem>? CommandExecuted;

    /// <summary>
    /// Walks all ribbon tabs, panels, and items to build the full command catalog.
    /// Must be called after Revit is fully initialized (ApplicationInitialized event).
    /// </summary>
    public void SnoopAll()
    {
        _commandsById.Clear();

        foreach (var info in EnumerateRibbonButtons())
        {
            var commandId = RevitCommandId.LookupCommandId(info.Id);
            if (commandId is null) continue;
            _commandsById.TryAdd(info.Id, new BrowserCommandItem(info, commandId));
        }
    }

    /// <summary>
    /// Starts listening for ribbon command executions and tab collection changes.
    /// Called after initial snooping is complete.
    /// </summary>
    public void StartTracking()
    {
        ComponentManager.ItemExecuted += OnItemExecuted;

        var ribbon = ComponentManager.Ribbon;
        if (ribbon?.Tabs is INotifyCollectionChanged tabs)
            tabs.CollectionChanged += OnTabsCollectionChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ComponentManager.ItemExecuted -= OnItemExecuted;

        var ribbon = ComponentManager.Ribbon;
        if (ribbon?.Tabs is INotifyCollectionChanged tabs)
            tabs.CollectionChanged -= OnTabsCollectionChanged;
    }

    private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset))
            return;

        foreach (var info in EnumerateRibbonButtons())
        {
            var commandId = RevitCommandId.LookupCommandId(info.Id);
            if (commandId is null) continue;
            _commandsById.TryAdd(info.Id, new BrowserCommandItem(info, commandId));
        }
    }

    private void OnItemExecuted(object? sender, RibbonItemExecutedEventArgs e)
    {
        var id = e.Item.Id.Replace("_RibbonListButton", string.Empty);
        if (_commandsById.TryGetValue(id, out var match))
            CommandExecuted?.Invoke(match);
    }

    private static IEnumerable<RibbonCommandInfo> EnumerateRibbonButtons()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon is null) yield break;

        foreach (var tab in ribbon.Tabs)
        {
            if (!tab.IsVisible) continue;
            foreach (var panel in tab.Panels)
            {
                foreach (var info in EnumeratePanel(panel.Source.Items, panel))
                    yield return info;
            }
        }
    }

    private static IEnumerable<RibbonCommandInfo> EnumeratePanel(
        RibbonItemCollection items,
        RibbonPanel panel)
    {
        foreach (var item in items)
        {
            foreach (var info in EnumerateItem(item, panel))
                yield return info;
        }
    }

    private static IEnumerable<RibbonCommandInfo> EnumerateItem(
        RibbonItem item,
        RibbonPanel panel)
    {
        switch (item)
        {
            case RibbonCommandItem cmd when IsValid(cmd):
                yield return new RibbonCommandInfo(cmd, panel);
                yield break;

            case RibbonRowPanel rowPanel:
                foreach (var info in EnumeratePanel(rowPanel.Items, panel))
                    yield return info;
                yield break;

            case RibbonSplitButton splitButton:
                foreach (var info in EnumeratePanel(splitButton.Items, panel))
                    yield return info;
                yield break;
        }
    }

    private static bool IsValid(RibbonCommandItem cmd) =>
        !string.IsNullOrEmpty(cmd.Id) &&
        (!string.IsNullOrEmpty(cmd.Name) || !string.IsNullOrEmpty(cmd.Text));
}
