using DevTools.NUnit.Transport.Contracts;

namespace DevTools.NUnit.Transport.Runtime;

public interface INUnitRuntimeSession : IDisposable
{
    string GenerationId { get; }

    NUnitDiscoverResponse Discover(NUnitDiscoverRequest request);

    NUnitRunResponse Run(
        NUnitRunRequest request,
        INUnitRuntimeEventSink eventSink,
        CancellationToken cancellationToken);

    void Cancel(Guid runId);
}

public interface INUnitRuntimeEventSink
{
    void Publish(NUnitRuntimeEvent runtimeEvent);
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRuntimeEvent(
    Guid RunId,
    string Kind,
    NUnitCaseResult? Case,
    string? Message,
    NUnitAttachment? Attachment);
