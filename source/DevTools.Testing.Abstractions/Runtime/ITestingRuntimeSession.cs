using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Runtime;

public interface ITestingRuntimeEventSink
{
    void Publish(TestEvent testingEvent);
}

public interface ITestingRuntimeSession : IDisposable
{
    string GenerationId { get; }

    TestRunResponse Run(
        TestRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken);

    void Cancel(Guid runId);
}
