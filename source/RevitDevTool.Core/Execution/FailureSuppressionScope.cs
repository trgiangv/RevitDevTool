using Autodesk.Revit.DB.Events;

namespace RevitDevTool.Core.Execution;

/// <summary>
/// Subscribes to <see cref="Autodesk.Revit.ApplicationServices.Application.FailuresProcessing"/>
/// and auto-resolves/rollbacks failures while active. Reference-counted for safe nesting.
/// </summary>
internal sealed class FailureSuppressionScope : IDisposable
{
    private static readonly Lock SyncRoot = new();
    private static int _refCount;
    private static EventHandler<FailuresProcessingEventArgs>? _handler;

    private readonly ExecutionGuardFeedback _feedback;
    private int _disposed;

    internal FailureSuppressionScope(ExecutionGuardFeedback feedback)
    {
        _feedback = feedback;

        lock (SyncRoot)
        {
            _refCount++;
            if (_refCount != 1) return;
            _handler = OnFailuresProcessing;
            RevitContext.Application.FailuresProcessing += _handler;
        }
    }

    private void OnFailuresProcessing(object? sender, FailuresProcessingEventArgs args)
    {
        var failuresAccessor = args.GetFailuresAccessor();
        var failureMessages = failuresAccessor.GetFailureMessages();

        if (failureMessages.Count == 0)
        {
            args.SetProcessingResult(FailureProcessingResult.Continue);
            return;
        }

        var hasUnresolvable = false;
        foreach (var message in failureMessages)
        {
            var severity = message.GetSeverity();

            if (severity == FailureSeverity.Warning)
            {
                failuresAccessor.DeleteWarning(message);
                _feedback.RecordWarningDismissed();
            }
            else
            {
                if (message.HasResolutions())
                {
                    failuresAccessor.ResolveFailure(message);
                    _feedback.RecordErrorResolved();
                }
                else
                {
                    hasUnresolvable = true;
                    var failureId = message.GetFailureDefinitionId().Guid.ToString();
                    _feedback.RecordRollback(failureId);
                }
            }
        }

        if (!hasUnresolvable)
        {
            args.SetProcessingResult(FailureProcessingResult.ProceedWithCommit);
            return;
        }

        var options = failuresAccessor.GetFailureHandlingOptions();
        options.SetClearAfterRollback(true);
        failuresAccessor.SetFailureHandlingOptions(options);
        args.SetProcessingResult(FailureProcessingResult.ProceedWithRollBack);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        lock (SyncRoot)
        {
            _refCount--;
            if (_refCount != 0 || _handler is null) return;
            RevitContext.Application.FailuresProcessing -= _handler;
            _handler = null;
        }
    }
}
