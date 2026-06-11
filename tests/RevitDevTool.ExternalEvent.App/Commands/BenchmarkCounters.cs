#if DEBUG
namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class BenchmarkCounters
{
    private const int PerTaskTimeoutMs = 30_000;

    private int _completed, _faulted, _timedOut, _cancelled;

    public int Completed => _completed;
    public int Faulted => _faulted;
    public int TimedOut => _timedOut;
    public int Cancelled => _cancelled;
    public int Total => _completed + _faulted + _timedOut + _cancelled;

    public void RecordSuccess() => Interlocked.Increment(ref _completed);
    public void RecordTimeout() => Interlocked.Increment(ref _timedOut);
    public void RecordFailure() => Interlocked.Increment(ref _faulted);
    public void RecordCancel() => Interlocked.Increment(ref _cancelled);

    public async Task AwaitAndRecord(Task task)
    {
        try
        {
            var winner = await Task.WhenAny(task, Task.Delay(PerTaskTimeoutMs));
            if (winner != task) { RecordTimeout(); return; }
            await task;
            RecordSuccess();
        }
        catch (OperationCanceledException) { RecordCancel(); }
        catch (TimeoutException) { RecordTimeout(); }
        catch { RecordFailure(); }
    }

    public async Task RunGuarded(Func<Task> action)
    {
        try
        {
            await action();
            RecordSuccess();
        }
        catch (TimeoutException) { RecordTimeout(); }
        catch (OperationCanceledException) { RecordCancel(); }
        catch { RecordFailure(); }
    }

    public BenchmarkResult ToResult(string adapter, BenchmarkCategory category, int requested, double wallMs)
    {
        return ToResult(adapter, BenchmarkSuite.CentralDispatcher, category, requested, wallMs);
    }

    public BenchmarkResult ToResult(string adapter, BenchmarkSuite suite, BenchmarkCategory category, int requested, double wallMs)
    {
        return new BenchmarkResult
        {
            Suite = suite,
            AdapterName = adapter,
            Category = category.ToString(),
            TotalRequested = requested,
            Completed = Completed,
            Faulted = Faulted,
            Cancelled = Cancelled,
            TimedOut = TimedOut,
            WallTimeMs = wallMs,
            ThroughputRps = wallMs > 0 ? Completed / (wallMs / 1000.0) : 0,
        };
    }
}
#endif
