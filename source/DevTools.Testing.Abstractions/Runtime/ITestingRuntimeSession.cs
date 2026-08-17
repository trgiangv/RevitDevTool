using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Runtime;

public interface ITestingRuntimeEventSink
{
    void Publish(TestingRuntimeEvent testingEvent);
}

public sealed record TestingRuntimeEvent(
    Guid RunId,
    string Kind,
    TestingCaseResult? Case,
    string? Message,
    TestingAttachment? Attachment,
    TestingCancellationState CancellationState);

public interface ITestingRuntimeSession : IDisposable
{
    string GenerationId { get; }

    TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken);

    void Cancel(Guid runId);
}
