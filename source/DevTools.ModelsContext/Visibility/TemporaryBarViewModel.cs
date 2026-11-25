using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Panel;
using System.Globalization;

namespace DevTools.ModelsContext.Visibility;

public sealed partial class TemporaryBarViewModel : ObservableObject
{
    private readonly VisibilityController _visibility;
    private readonly PanelRuntime _runtime;

    internal TemporaryBarViewModel(PanelRuntime runtime, VisibilityController visibility)
    {
        _runtime = runtime;
        _visibility = visibility;
        _visibility.Changed += (_, _) => _ = _runtime.RunAsync(() => RefreshAsync(CancellationToken.None));
    }

    public bool IsVisible => IsTemporaryActive || ChangeCount > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text))]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    public partial int ChangeCount { get; private set; }

    public string Text => ChangeCount == 1
        ? "Temporary · 1 change"
        : string.Format(CultureInfo.CurrentCulture, "Temporary · {0:N0} changes", ChangeCount);

    [ObservableProperty]
    public partial bool IsConfirmingKeep { get; private set; }

    public string ConfirmText => string.Format(CultureInfo.CurrentCulture,
        "{0:N0} link elements stay hidden in this view. Show them later with Reveal Hidden Elements.",
        LinkHideCount);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private partial bool IsTemporaryActive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmText))]
    private partial int LinkHideCount { get; set; }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var active = await _runtime.Services.ViewActions.ReadActiveViewAsync(cancellationToken);
        IsTemporaryActive = active?.IsTemporary == true;
        ChangeCount = _visibility.Journal.ChangeCount;
    }

    [RelayCommand]
    private Task KeepAsync()
    {
        LinkHideCount = _visibility.LinkHideCount;
        if (LinkHideCount > 0)
        {
            IsConfirmingKeep = true;
            return Task.CompletedTask;
        }

        return _runtime.RunAsync(() => _visibility.KeepAsync(CancellationToken.None));
    }

    [RelayCommand]
    private Task ConfirmKeepAsync()
    {
        IsConfirmingKeep = false;
        return _runtime.RunAsync(() => _visibility.KeepAsync(CancellationToken.None));
    }

    [RelayCommand]
    private void CancelKeep()
    {
        IsConfirmingKeep = false;
    }

    [RelayCommand]
    private Task ResetAsync()
    {
        IsConfirmingKeep = false;
        return _runtime.RunAsync(() => _visibility.ResetAsync(CancellationToken.None));
    }
}
