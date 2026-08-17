using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Providers;

public interface ITestingEventSink
{
    void Publish(TestingEvent testingEvent);
}

public interface IHostTestFrameworkProvider
{
    string FrameworkId { get; }

    TestingRunResponse Run(
        TestingRunRequest request,
        ITestingEventSink eventSink,
        CancellationToken cancellationToken);

    bool Cancel(Guid runId);
}
