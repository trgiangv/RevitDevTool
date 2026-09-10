using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Runtime;

public interface ITestingRuntimeEventSink
{
    void Publish(TestingEvent testingEvent);
}

public interface ITestingRuntimeSession : IDisposable
{
    string GenerationId { get; }

    TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken);

    void Cancel(Guid runId);
}
