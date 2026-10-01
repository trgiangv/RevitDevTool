using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Runtime;

public interface ITestEventSink
{
    void Publish(TestEvent testingEvent);
}

public interface ITestingRuntimeSession : IDisposable
{
    string GenerationId { get; }

    TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken);

    void Cancel(Guid runId);
}
