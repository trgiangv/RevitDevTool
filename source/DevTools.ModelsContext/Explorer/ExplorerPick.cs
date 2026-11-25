using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Selection;

namespace DevTools.ModelsContext.Explorer;

public sealed partial class ExplorerPick : ObservableObject
{
    public const string PickTip = "Pick in checked links";

    private readonly Func<IReadOnlyList<long>> _categories;
    private readonly PickController _controller;
    private readonly Func<IReadOnlyList<DocumentKey>> _documents;
    private readonly PanelRuntime _runtime;

    internal ExplorerPick(PanelRuntime runtime, Func<IReadOnlyList<DocumentKey>> documents,
        Func<IReadOnlyList<long>> categories)
    {
        _runtime = runtime;
        _documents = documents;
        _categories = categories;
        _controller = new PickController(runtime.Services.Selection);
        _controller.PickingChanged += (_, _) => OnPickingChanged();
    }

    public bool IsPicking => _controller.IsPicking;

    public string? DisabledReason => PickController.DisabledReason(_documents(), _categories());

    public string ToolTip => DisabledReason ?? PickTip;

    [ObservableProperty]
    public partial string? Status { get; private set; }

    internal void Refresh()
    {
        OnPropertyChanged(nameof(DisabledReason));
        OnPropertyChanged(nameof(ToolTip));
        PickCommand.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        Status = null;
    }

    private bool CanPick()
    {
        return _controller.CanPick(_documents(), _categories());
    }

    [RelayCommand(CanExecute = nameof(CanPick))]
    private Task PickAsync()
    {
        return _runtime.RunAsync(async () =>
        {
            var result = await _controller.PickAsync(_documents(), _categories(), CancellationToken.None);
            Status = PickController.StatusOf(result);
        });
    }

    private void OnPickingChanged()
    {
        if (IsPicking)
        {
            Status = PickController.PickingCue;
        }

        OnPropertyChanged(nameof(IsPicking));
        PickCommand.NotifyCanExecuteChanged();
    }
}
