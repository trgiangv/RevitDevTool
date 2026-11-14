using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.ViewModels;

public enum MenuAction
{
    SelectInHost,
    Zoom,
    SectionBox,
    Isolate,
    HideShow,
    Halftone,
    Color,
    ResetView
}

public sealed partial class MenuItemViewModel : ObservableObject
{
    public const string LinkCommandsNote = "Goes through link commands";

    internal MenuItemViewModel(MenuAction? action, string text, string iconKey, string? shortcut, Func<Task>? execute)
    {
        Action = action;
        Text = text;
        IconKey = iconKey;
        Shortcut = shortcut;
        Command = new AsyncRelayCommand(execute ?? (() => Task.CompletedTask), () => IsEnabled);
    }

    public MenuAction? Action { get; }

    public bool IsSeparator => Action is null;

    [ObservableProperty]
    public partial string Text { get; internal set; }

    [ObservableProperty]
    public partial string IconKey { get; internal set; }

    public string? Shortcut { get; }

    [ObservableProperty]
    public partial bool IsVisible { get; internal set; } = true;

    public bool IsEnabled => DisabledReason is null;

    public string? ToolTipText => DisabledReason ?? (UsesReferenceCommands ? LinkCommandsNote : null);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled))]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    [NotifyCanExecuteChangedFor(nameof(Command))]
    public partial string? DisabledReason { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    public partial bool UsesReferenceCommands { get; internal set; }

    public IAsyncRelayCommand Command { get; }

    internal static MenuItemViewModel Separator()
    {
        return new MenuItemViewModel(null, string.Empty, string.Empty, null, null);
    }
}

internal sealed record MenuTarget(
    IReadOnlyList<TreeRow> Rows,
    IReadOnlyList<ElementRef> Elements,
    Func<IReadOnlyList<ElementRef>> Resolve,
    bool IsSyncing,
    bool IsHidden,
    CancellationToken CancellationToken)
{
    public bool IsEmpty => Rows.Count == 0 && Elements.Count == 0;

    public VisibilityRequest Request(VisibilityAction action, RgbColor? color = null)
    {
        return new VisibilityRequest(action, Rows, Elements, color);
    }
}

public sealed partial class ContextMenuViewModel : ObservableObject
{
    public const string NeedsThreeDView = "Section box needs a 3D view.";

    private readonly Dictionary<MenuAction, MenuItemViewModel> _byAction = [];
    private readonly MenuItemViewModel _resetSeparator;
    private readonly PanelRuntime _runtime;
    private readonly TemporaryBarViewModel _temporaryBar;
    private readonly VisibilityController _visibility;
    private MenuTarget? _target;

    internal ContextMenuViewModel(PanelRuntime runtime, VisibilityController visibility, TemporaryBarViewModel temporaryBar)
    {
        _runtime = runtime;
        _visibility = visibility;
        _temporaryBar = temporaryBar;
        _resetSeparator = MenuItemViewModel.Separator();
        Items =
        [
            Add(MenuAction.SelectInHost, "Select in " + runtime.Services.Source.HostName, "Select", null, SelectAsync),
            Add(MenuAction.Zoom, "Zoom", "Zoom", "Z", ZoomAsync),
            Add(MenuAction.SectionBox, "Section box", "SectionBox", "B", SectionBoxAsync),
            MenuItemViewModel.Separator(),
            Add(MenuAction.Isolate, "Isolate", "Isolate", "I", () => RunAsync(VisibilityAction.Isolate)),
            Add(MenuAction.HideShow, "Hide", "Hide", "H", HideShowAsync),
            Add(MenuAction.Halftone, "Halftone", "Halftone", null, () => RunAsync(VisibilityAction.Halftone)),
            Add(MenuAction.Color, "Color…", "Color", null, RequestColorAsync),
            _resetSeparator,
            Add(MenuAction.ResetView, "Reset view", "Reset", null, () => _temporaryBar.ResetCommand.ExecuteAsync(null))
        ];
    }

    public event EventHandler? ColorRequested;

    public IReadOnlyList<MenuItemViewModel> Items { get; }

    [ObservableProperty]
    public partial string? Message { get; private set; }

    public MenuItemViewModel ResetSeparator => _resetSeparator;

    public MenuItemViewModel this[MenuAction action] => _byAction[action];

    public async Task<bool> RunShortcutAsync(string key)
    {
        var item = Items.FirstOrDefault(item =>
            string.Equals(item.Shortcut, key, StringComparison.OrdinalIgnoreCase));
        if (_target is null || item is not { IsVisible: true, IsEnabled: true })
        {
            return false;
        }

        await item.Command.ExecuteAsync(null);
        return true;
    }

    internal void Open(MenuTarget target)
    {
        _target = target;
        Message = null;
        var select = this[MenuAction.SelectInHost];
        select.IsVisible = !target.IsSyncing;
        var hideShow = this[MenuAction.HideShow];
        hideShow.Text = target.IsHidden ? "Show" : "Hide";
        hideShow.IconKey = target.IsHidden ? "Show" : "Hide";
        foreach (var item in Items.Where(item => !item.IsSeparator))
        {
            var (reason, usesReferences) = Evaluate(target, item.Action!.Value);
            item.DisabledReason = reason;
            item.UsesReferenceCommands = usesReferences;
        }

        var reset = _temporaryBar.IsVisible;
        this[MenuAction.ResetView].IsVisible = reset;
        _resetSeparator.IsVisible = reset;
    }

    internal void Refresh()
    {
        if (_target is { } target)
        {
            Open(target);
        }
    }

    internal string? ReasonFor(MenuTarget target, MenuAction action)
    {
        return Evaluate(target, action).Reason;
    }

    private (string? Reason, bool UsesReferenceCommands) Evaluate(MenuTarget target, MenuAction action)
    {
        if (_runtime.IsBusy)
        {
            return (_runtime.BusyText, false);
        }

        var empty = target.IsEmpty ? VisibilityReasons.Nothing : null;
        switch (action)
        {
            case MenuAction.SelectInHost or MenuAction.Zoom:
                return (empty, false);
            case MenuAction.SectionBox:
                return (empty ?? (_visibility.View?.Is3D == true ? null : NeedsThreeDView), false);
            case MenuAction.ResetView:
                return (null, false);
            default:
                var check = _visibility.Check(target.Request(action switch
                {
                    MenuAction.Isolate => VisibilityAction.Isolate,
                    MenuAction.HideShow => target.IsHidden ? VisibilityAction.Show : VisibilityAction.Hide,
                    MenuAction.Halftone => VisibilityAction.Halftone,
                    _ => VisibilityAction.Color
                }));
                return (check.DisabledReason, check.UsesReferenceCommands);
        }
    }

    private MenuItemViewModel Add(MenuAction action, string text, string icon, string? shortcut, Func<Task> execute)
    {
        var item = new MenuItemViewModel(action, text, icon, shortcut, () => _runtime.RunAsync(execute));
        _byAction.Add(action, item);
        return item;
    }

    private async Task SelectAsync()
    {
        if (_target is { } target)
        {
            await _runtime.SelectAsync(target.Resolve(), target.CancellationToken);
        }
    }

    private async Task ZoomAsync()
    {
        if (_target is { } target)
        {
            await _runtime.Services.ViewActions.ZoomAsync(target.Resolve(), target.CancellationToken);
        }
    }

    private async Task SectionBoxAsync()
    {
        if (_target is { } target)
        {
            await _runtime.Services.ViewActions.SectionBoxAsync(target.Resolve(), target.CancellationToken);
        }
    }

    private Task HideShowAsync()
    {
        return RunAsync(_target?.IsHidden == true ? VisibilityAction.Show : VisibilityAction.Hide);
    }

    private Task RequestColorAsync()
    {
        ColorRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ApplyColorAsync(RgbColor? color)
    {
        return _runtime.RunAsync(() => color is { } picked ? RunAsync(VisibilityAction.Color, picked) : Task.CompletedTask);
    }

    private async Task RunAsync(VisibilityAction action, RgbColor? color = null)
    {
        if (_target is { } target)
        {
            Message = await _visibility.RunAsync(target.Request(action, color), target.CancellationToken);
        }
    }
}
