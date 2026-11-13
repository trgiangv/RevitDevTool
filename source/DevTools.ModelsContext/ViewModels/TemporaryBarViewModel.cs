using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace DevTools.ModelsContext.ViewModels;

public sealed class TemporaryBarViewModel : ObservableObject
{
    private readonly VisibilityController _visibility;
    private readonly PanelRuntime _runtime;
    private int _changeCount;
    private bool _isConfirmingKeep;
    private bool _isTemporaryActive;
    private int _pendingLinkChanges;

    internal TemporaryBarViewModel(PanelRuntime runtime, VisibilityController visibility)
    {
        _runtime = runtime;
        _visibility = visibility;
        KeepCommand = new AsyncRelayCommand(KeepAsync);
        ConfirmKeepCommand = new AsyncRelayCommand(ConfirmKeepAsync);
        CancelKeepCommand = new RelayCommand(() => IsConfirmingKeep = false);
        ResetCommand = new AsyncRelayCommand(ResetAsync);
        _visibility.Changed += (_, _) => _ = _runtime.RunAsync(() => RefreshAsync(CancellationToken.None));
    }

    public IAsyncRelayCommand KeepCommand { get; }

    public IAsyncRelayCommand ConfirmKeepCommand { get; }

    public IRelayCommand CancelKeepCommand { get; }

    public IAsyncRelayCommand ResetCommand { get; }

    public bool IsVisible => _isTemporaryActive || _changeCount > 0;

    public int ChangeCount
    {
        get => _changeCount;
        private set
        {
            if (SetProperty(ref _changeCount, value))
            {
                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    public string Text => ChangeCount == 1
        ? "Temporary · 1 change"
        : string.Format(CultureInfo.CurrentCulture, "Temporary · {0:N0} changes", ChangeCount);

    public bool IsConfirmingKeep
    {
        get => _isConfirmingKeep;
        private set => SetProperty(ref _isConfirmingKeep, value);
    }

    public string ConfirmText => string.Format(CultureInfo.CurrentCulture,
        "{0:N0} hidden link elements become permanent. Undo them later with Reveal Hidden Elements.",
        _pendingLinkChanges);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var state = await _runtime.Services.ViewActions.ReadTemporaryStateAsync(cancellationToken);
        _isTemporaryActive = state?.IsActive == true;
        ChangeCount = _visibility.Journal.ChangeCount;
        OnPropertyChanged(nameof(IsVisible));
    }

    private async Task KeepAsync()
    {
        _pendingLinkChanges = _visibility.PendingLinkChanges;
        if (_pendingLinkChanges > 0)
        {
            OnPropertyChanged(nameof(ConfirmText));
            IsConfirmingKeep = true;
            return;
        }

        await _visibility.KeepAsync(CancellationToken.None);
    }

    private async Task ConfirmKeepAsync()
    {
        IsConfirmingKeep = false;
        await _visibility.KeepAsync(CancellationToken.None);
    }

    private async Task ResetAsync()
    {
        IsConfirmingKeep = false;
        await _visibility.ResetAsync(CancellationToken.None);
    }
}
