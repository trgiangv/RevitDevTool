using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Providers;

public interface ITestEventSink
{
    void Publish(TestEvent testingEvent);
}

public interface ITestFrameworkProvider
{
    TestFrameworkId FrameworkId { get; }

    TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken);

    bool Cancel(Guid runId);
}
