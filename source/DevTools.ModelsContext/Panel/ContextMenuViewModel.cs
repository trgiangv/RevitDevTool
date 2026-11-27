using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Panel;

public enum MenuAction
{
    SelectInHost,
    Zoom,
    SectionBox,
    Isolate,
    HideShow,
    Halftone,
    Color,
    WhyHidden
}

public sealed partial class MenuItemViewModel : ObservableObject
{
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

    public string? ToolTipText => DisabledReason ?? Note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled))]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    [NotifyCanExecuteChangedFor(nameof(Command))]
    public partial string? DisabledReason { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    public partial string? Note { get; internal set; }

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
    CancellationToken CancellationToken,
    FilterDefinition? Filter = null)
{
    public bool IsEmpty => Rows.Count == 0 && Elements.Count == 0;

    public VisibilityRequest Request(VisibilityAction action, RgbColor? color = null)
    {
        return new VisibilityRequest(action, Rows, Elements, color, Filter);
    }
}

public sealed partial class ContextMenuViewModel : ObservableObject
{
    public const string NeedsOneElement = "One element only.";

    private readonly Dictionary<MenuAction, MenuItemViewModel> _byAction = [];
    private readonly PanelRuntime _runtime;
    private readonly VisibilityController _visibility;
    private readonly WhyHiddenStripViewModel _whyHidden;
    private MenuTarget? _target;

    internal ContextMenuViewModel(PanelRuntime runtime, VisibilityController visibility, WhyHiddenStripViewModel whyHidden)
    {
        _runtime = runtime;
        _visibility = visibility;
        _whyHidden = whyHidden;
        Items =
        [
            Add(MenuAction.SelectInHost, "Select", "Select", null, SelectAsync),
            Add(MenuAction.Zoom, "Zoom", "Zoom", "Z", ZoomAsync),
            Add(MenuAction.SectionBox, "Section box", "SectionBox", "B", SectionBoxAsync),
            MenuItemViewModel.Separator(),
            Add(MenuAction.Isolate, "Isolate", "Isolate", "I", () => RunAsync(VisibilityAction.Isolate)),
            Add(MenuAction.HideShow, "Hide", "Hide", "H", HideShowAsync),
            Add(MenuAction.Halftone, "Halftone", "Halftone", null, () => RunAsync(VisibilityAction.Halftone)),
            Add(MenuAction.Color, "Color", "Color", null, RequestColorAsync),
            MenuItemViewModel.Separator(),
            Add(MenuAction.WhyHidden, "Why hidden?", "WhyHidden", null, WhyHiddenAsync)
        ];
    }

    public event EventHandler? ColorRequested;

    public event EventHandler? Opened;

    public IReadOnlyList<MenuItemViewModel> Items { get; }

    [ObservableProperty]
    public partial string? Message { get; private set; }

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

    internal Task SelectAndZoomAsync()
    {
        if (_target is not { } target || Evaluate(target, MenuAction.Zoom).DisabledReason is not null)
        {
            return Task.CompletedTask;
        }

        return _runtime.RunAsync(async () =>
        {
            await SelectAsync();
            await ZoomAsync();
        });
    }

    internal void Open(MenuTarget target)
    {
        _target = target;
        Message = null;
        Opened?.Invoke(this, EventArgs.Empty);
        var select = this[MenuAction.SelectInHost];
        select.IsVisible = !target.IsSyncing;
        var hideShow = this[MenuAction.HideShow];
        hideShow.Text = target.IsHidden ? "Show" : "Hide";
        hideShow.IconKey = target.IsHidden ? "Show" : "Hide";
        foreach (var item in Items.Where(item => !item.IsSeparator))
        {
            var state = Evaluate(target, item.Action!.Value);
            item.DisabledReason = state.DisabledReason;
            item.Note = state.Note;
        }
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
        return Evaluate(target, action).DisabledReason;
    }

    private ActionState Evaluate(MenuTarget target, MenuAction action)
    {
        if (_runtime.IsBusy)
        {
            return new ActionState(_runtime.BusyText);
        }

        return action switch
        {
            MenuAction.SelectInHost or MenuAction.Zoom => new ActionState(EmptyReason(target)),
            MenuAction.SectionBox => new ActionState(EmptyReason(target) ?? SectionBoxReason()),
            MenuAction.WhyHidden => new ActionState(WhyHiddenReason(target)),
            _ => _visibility.Check(target.Request(VisibilityActionOf(action, target.IsHidden)))
        };
    }

    private static string? EmptyReason(MenuTarget target)
    {
        return target.IsEmpty ? VisibilityText.Nothing : null;
    }

    private string? WhyHiddenReason(MenuTarget target)
    {
        if (SingleElementOf(target) is not { } element)
        {
            return NeedsOneElement;
        }

        return IsShown(element) ? VisibilityText.ShownInView : null;
    }

    private bool IsShown(ElementRef element)
    {
        var data = _runtime.Data;
        if (data.Scope != ScopeKind.ActiveView
            || data.Find(element.Document) is not { } document
            || !document.InScope(element.Id)
            || _visibility.IsHidden(element)
            || _visibility.Hides(element))
        {
            return false;
        }

        return !document.Snapshot.TryGetRow(element.Id, out var row)
               || !_visibility.HidesCategory(document.Snapshot.Groups[row.GroupIndex].GroupId);
    }

    private static ElementRef? SingleElementOf(MenuTarget target)
    {
        return target switch
        {
            { Rows.Count: 0, Elements: [var element] } => element,
            { Elements.Count: 0, Rows: [{ Kind: TreeRowKind.Instance, Document: { } document } row] } => new ElementRef(document, row.Id),
            _ => null
        };
    }

    private string? SectionBoxReason()
    {
        return _visibility.View?.Is3D == true ? null : VisibilityText.Needs3DView;
    }

    private static VisibilityAction VisibilityActionOf(MenuAction action, bool isHidden)
    {
        return action switch
        {
            MenuAction.Isolate => VisibilityAction.Isolate,
            MenuAction.HideShow => isHidden ? VisibilityAction.Show : VisibilityAction.Hide,
            MenuAction.Halftone => VisibilityAction.Halftone,
            _ => VisibilityAction.Color
        };
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

    private Task WhyHiddenAsync()
    {
        return _target is { } target && SingleElementOf(target) is { } element
            ? _whyHidden.OpenAsync(element)
            : Task.CompletedTask;
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
            Message = await _visibility.RunAsync(target.Request(action, color));
        }
    }
}
