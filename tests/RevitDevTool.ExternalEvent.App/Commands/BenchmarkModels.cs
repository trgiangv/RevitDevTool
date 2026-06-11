#if DEBUG
namespace RevitDevTool.ExternalEvent.App.Commands;

internal enum BenchmarkSuite
{
    CentralDispatcher,
    InContextEventReuse,
}

internal enum BenchmarkCategory
{
    // Suite 1 — Central Dispatcher
    SequentialLatency,
    ProducerSequential,
    TrueBurst,
    SustainedLoad,
    DirectInvocation,
    NestedReentry,
    CancellationLifecycle,
    ErrorPropagation,

    // Suite 2 — In-Context Event Reuse
    SequentialRaise,
    ConcurrentRaise,
}

internal enum WorkloadProfile
{
    NoOp,
    LightRevitRead,
    TransactionRollback,
}

internal sealed class BenchmarkResult
{
    public BenchmarkSuite Suite { get; set; } = BenchmarkSuite.CentralDispatcher;
    public string AdapterName { get; init; } = "";
    public string Category { get; set; } = "";

    public int TotalRequested { get; set; }
    public int Completed { get; set; }
    public int Faulted { get; set; }
    public int Cancelled { get; set; }
    public int TimedOut { get; set; }

    public double WallTimeMs { get; set; }
    public double ThroughputRps { get; set; }

    public PercentileStats? EnqueueLatency { get; set; }
    public PercentileStats? WaitLatency { get; set; }
    public PercentileStats? ExecutionDuration { get; set; }
    public PercentileStats? TotalLatency { get; set; }

    public string? Notes { get; set; }
}
#endif
