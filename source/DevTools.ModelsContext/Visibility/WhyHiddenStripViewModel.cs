using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Visibility;

public sealed partial class WhyHiddenStripViewModel : ObservableObject
{
    public const string CheckingText = "Checking…";
    public const string FailedText = "Check failed";
    public const string FixText = "Show";

    private readonly PanelRuntime _runtime;
    private readonly VisibilityController _visibility;
    private CancellationTokenSource? _call;

    internal WhyHiddenStripViewModel(PanelRuntime runtime, VisibilityController visibility)
    {
        _runtime = runtime;
        _visibility = visibility;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    public partial ElementRef? Element { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFix))]
    [NotifyCanExecuteChangedFor(nameof(FixCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(First))]
    [NotifyPropertyChangedFor(nameof(HasFix))]
    [NotifyPropertyChangedFor(nameof(HasMore))]
    [NotifyCanExecuteChangedFor(nameof(FixCommand))]
    public partial IReadOnlyList<HiddenCause> Causes { get; private set; } = [];

    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? Cue { get; private set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public HiddenCause? First => Causes.Count > 0 ? Causes[0] : null;

    public bool HasFix => !IsBusy && First is { Fix: not HiddenFix.None };

    public bool HasMore => Causes.Count > 1;

    public Task OpenAsync(ElementRef element)
    {
        var token = Begin(element);
        return _runtime.RunAsync(() => ExplainAsync(element, token));
    }

    public Task FollowAsync(ElementRef? selected)
    {
        if (selected is not { } element)
        {
            if (IsOpen)
            {
                Close();
            }

            return Task.CompletedTask;
        }

        return IsOpen && Element == element ? Task.CompletedTask : OpenAsync(element);
    }

    [RelayCommand(CanExecute = nameof(IsOpen))]
    public void Close()
    {
        Cancel();
        IsOpen = false;
        IsBusy = false;
        IsExpanded = false;
        Element = null;
        Causes = [];
        Text = string.Empty;
        Cue = null;
    }

    [RelayCommand(CanExecute = nameof(HasFix))]
    private Task FixAsync()
    {
        if (Element is not { } element || First is not { Fix: not HiddenFix.None })
        {
            return Task.CompletedTask;
        }

        var token = Begin(element);
        return _runtime.RunAsync(() => FixAndExplainAsync(element, token));
    }

    private CancellationToken Begin(ElementRef element)
    {
        Cancel();
        _call = new CancellationTokenSource();
        if (Element != element)
        {
            IsExpanded = false;
        }

        Element = element;
        IsOpen = true;
        Cue = null;
        return _call.Token;
    }

    private void Cancel()
    {
        if (_call is { } call)
        {
            _call = null;
            call.Cancel();
            call.Dispose();
        }
    }

    private async Task FixAndExplainAsync(ElementRef element, CancellationToken token)
    {
        IsBusy = true;
        string? reason;
        try
        {
            reason = await _visibility.RunAsync(VisibilityRequest.ForElements(VisibilityAction.Show, [element]));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _runtime.Logger.Failed(exception, exception.Message);
            reason = exception.Message;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        if (reason is not null)
        {
            IsBusy = false;
            Cue = reason;
            return;
        }

        if (await ExplainAsync(element, token) is { Found: false } && !token.IsCancellationRequested)
        {
            Close();
        }
    }

    private async Task<WhyHiddenResult?> ExplainAsync(ElementRef element, CancellationToken token)
    {
        if (_runtime.IsBusy)
        {
            Show([], _runtime.BusyText);
            return null;
        }

        IsBusy = true;
        Text = CheckingText;
        try
        {
            var result = await _runtime.Services.Explainer.ExplainAsync(element, token);
            if (token.IsCancellationRequested)
            {
                return null;
            }

            result = result.WithJournal(_visibility.Journal, element);
            Show(result.Causes, result.First.Text);
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            if (token.IsCancellationRequested)
            {
                return null;
            }

            _runtime.Logger.Failed(exception, exception.Message);
            Show([], FailedText);
            Cue = exception.Message;
            return null;
        }
    }

    private void Show(IReadOnlyList<HiddenCause> causes, string text)
    {
        Causes = causes;
        Text = text;
        IsBusy = false;
        if (causes.Count < 2)
        {
            IsExpanded = false;
        }
    }

}
