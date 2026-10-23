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

    private int _disposed;

    internal FailureSuppressionScope(ExecutionGuardFeedback feedback)
    {
        Acquire(feedback);
    }

    private static void Acquire(ExecutionGuardFeedback feedback)
    {
        lock (SyncRoot)
        {
            _refCount++;
            if (_refCount != 1) return;
            _handler = (_, args) => OnFailuresProcessing(feedback, args);
            RevitContext.Application.FailuresProcessing += _handler;
        }
    }

    private static void OnFailuresProcessing(ExecutionGuardFeedback feedback, FailuresProcessingEventArgs args)
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
                feedback.RecordWarningDismissed();
            }
            else
            {
                if (message.HasResolutions())
                {
                    failuresAccessor.ResolveFailure(message);
                    feedback.RecordErrorResolved();
                }
                else
                {
                    hasUnresolvable = true;
                    var failureId = message.GetFailureDefinitionId().Guid.ToString();
                    feedback.RecordRollback(failureId);
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

        Release();
    }

    private static void Release()
    {
        lock (SyncRoot)
        {
            _refCount--;
            if (_refCount != 0 || _handler is null) return;
            RevitContext.Application.FailuresProcessing -= _handler;
            _handler = null;
        }
    }
}
