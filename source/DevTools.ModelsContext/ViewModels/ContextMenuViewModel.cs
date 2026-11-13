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

public sealed class MenuItemViewModel : ObservableObject
{
    private string? _disabledReason;
    private string _iconKey;
    private bool _isVisible = true;
    private string _text;
    private bool _usesReferenceCommands;

    internal MenuItemViewModel(MenuAction? action, string text, string iconKey, string? shortcut, Func<Task>? execute)
    {
        Action = action;
        _text = text;
        _iconKey = iconKey;
        Shortcut = shortcut;
        Command = new AsyncRelayCommand(execute ?? (() => Task.CompletedTask), () => IsEnabled);
    }

    public MenuAction? Action { get; }

    public bool IsSeparator => Action is null;

    public string Text
    {
        get => _text;
        internal set => SetProperty(ref _text, value);
    }

    public string IconKey
    {
        get => _iconKey;
        internal set => SetProperty(ref _iconKey, value);
    }

    public string? Shortcut { get; }

    public bool IsVisible
    {
        get => _isVisible;
        internal set => SetProperty(ref _isVisible, value);
    }

    public bool IsEnabled => DisabledReason is null;

    public string? DisabledReason
    {
        get => _disabledReason;
        internal set
        {
            if (SetProperty(ref _disabledReason, value))
            {
                OnPropertyChanged(nameof(IsEnabled));
                Command.NotifyCanExecuteChanged();
            }
        }
    }

    public bool UsesReferenceCommands
    {
        get => _usesReferenceCommands;
        internal set => SetProperty(ref _usesReferenceCommands, value);
    }

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

public sealed class ContextMenuViewModel : ObservableObject
{
    public const string NeedsThreeDView = "Section box needs a 3D view.";

    private readonly Dictionary<MenuAction, MenuItemViewModel> _byAction = [];
    private readonly MenuItemViewModel _resetSeparator;
    private readonly PanelRuntime _runtime;
    private readonly TemporaryBarViewModel _temporaryBar;
    private readonly VisibilityController _visibility;
    private string? _message;
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
        ApplyColorCommand = new AsyncRelayCommand<RgbColor?>(color => _runtime.RunAsync(() => ApplyColorAsync(color)));
    }

    public event EventHandler? ColorRequested;

    public IReadOnlyList<MenuItemViewModel> Items { get; }

    public IAsyncRelayCommand<RgbColor?> ApplyColorCommand { get; }

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

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
        var empty = target.IsEmpty ? VisibilityReasons.Nothing : null;
        var select = this[MenuAction.SelectInHost];
        select.IsVisible = !target.IsSyncing;
        select.DisabledReason = empty;
        this[MenuAction.Zoom].DisabledReason = empty;
        this[MenuAction.SectionBox].DisabledReason = empty ?? (_visibility.View?.Is3D == true ? null : NeedsThreeDView);

        var hideShow = this[MenuAction.HideShow];
        hideShow.Text = target.IsHidden ? "Show" : "Hide";
        hideShow.IconKey = target.IsHidden ? "Show" : "Hide";
        Plan(MenuAction.Isolate, VisibilityAction.Isolate);
        Plan(MenuAction.HideShow, target.IsHidden ? VisibilityAction.Show : VisibilityAction.Hide);
        Plan(MenuAction.Halftone, VisibilityAction.Halftone);
        Plan(MenuAction.Color, VisibilityAction.Color);

        var reset = _temporaryBar.IsVisible;
        this[MenuAction.ResetView].IsVisible = reset;
        _resetSeparator.IsVisible = reset;
        this[MenuAction.ResetView].DisabledReason = null;
        if (_runtime.IsBusy)
        {
            foreach (var item in Items.Where(item => !item.IsSeparator))
            {
                item.DisabledReason = _runtime.BusyText;
            }
        }
    }

    internal void Refresh()
    {
        if (_target is { } target)
        {
            Open(target);
        }
    }

    private void Plan(MenuAction item, VisibilityAction action)
    {
        var plan = _visibility.Plan(_target!.Request(action));
        this[item].DisabledReason = plan.DisabledReason;
        this[item].UsesReferenceCommands = plan.UsesReferenceCommands;
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

    private async Task ApplyColorAsync(RgbColor? color)
    {
        if (color is { } picked)
        {
            await RunAsync(VisibilityAction.Color, picked);
        }
    }

    private async Task RunAsync(VisibilityAction action, RgbColor? color = null)
    {
        if (_target is { } target)
        {
            Message = await _visibility.RunAsync(target.Request(action, color), target.CancellationToken);
        }
    }
}
