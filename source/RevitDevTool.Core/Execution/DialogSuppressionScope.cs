using Autodesk.Revit.UI.Events;

namespace RevitDevTool.Core.Execution;

/// <summary>
/// Subscribes to <see cref="UIApplication.DialogBoxShowing"/> and auto-dismisses
/// all dialogs while the scope is active. Reference-counted for safe nesting.
/// </summary>
internal sealed class DialogSuppressionScope : IDisposable
{
    private static readonly Lock SyncRoot = new();
    private static int _refCount;
    private static EventHandler<DialogBoxShowingEventArgs>? _handler;

    private readonly ExecutionGuardFeedback _feedback;
    private int _disposed;

    internal DialogSuppressionScope(ExecutionGuardFeedback feedback)
    {
        _feedback = feedback;

        lock (SyncRoot)
        {
            _refCount++;
            if (_refCount != 1) return;
            _handler = OnDialogBoxShowing;
            RevitContext.UiApplication.DialogBoxShowing += _handler;
        }
    }

    private void OnDialogBoxShowing(object? sender, DialogBoxShowingEventArgs args)
    {
        var overrideCode = ResolveOverrideResult(args.DialogId);
        args.OverrideResult(overrideCode);
        _feedback.RecordDialogSuppressed();
    }

    private static int ResolveOverrideResult(string dialogId)
    {
        return dialogId switch
        {
            "TaskDialog_Save_File" => (int)TaskDialogResult.No,
            "TaskDialog_Really_Print_Or_Export_Temp_View_Modes" => (int)TaskDialogResult.No,
            "TaskDialog_Unresolved_References" => (int)TaskDialogResult.No,
            "TaskDialog_Save_Family" => (int)TaskDialogResult.No,
            _ => 1,
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        lock (SyncRoot)
        {
            _refCount--;
            if (_refCount == 0 && _handler is not null)
            {
                RevitContext.UiApplication.DialogBoxShowing -= _handler;
                _handler = null;
            }
        }
    }
}
