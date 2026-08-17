using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Runtime;
using DevTools.NUnit.Host;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.NUnit.Host.Loading;

/// <summary>Adapts the NUnit load-context factories to the neutral session contract.</summary>
internal sealed class NUnitTestingRuntimeSessionFactory(INUnitRuntimeSessionFactory inner) : ITestingRuntimeSessionFactory
{
    private readonly INUnitRuntimeSessionFactory _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public ITestingRuntimeSession Create(TestingGenerationManifest generation) =>
        new NUnitTestingRuntimeSessionAdapter(
            _inner.Create(NUnitGenerationManifestAdapter.ToNUnit(generation)),
            generation.ShadowAssemblyPath);
}

internal sealed class NUnitTestingRuntimeSessionAdapter(INUnitRuntimeSession inner, string shadowAssemblyPath) :
    ITestingRuntimeSession,
    ITestingRuntimeSessionRetirementDiagnostics
{
    private readonly INUnitRuntimeSession _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly string _shadowAssemblyPath = shadowAssemblyPath ?? throw new ArgumentNullException(nameof(shadowAssemblyPath));
    private readonly string _generationId = inner.GenerationId;

    public string GenerationId => _generationId;

    public TestingRunResponse Run(TestingRunRequest request, ITestingRuntimeEventSink eventSink, CancellationToken cancellationToken)
    {
        var response = _inner.Run(
            new NUnitRunRequest(request.RunId, _shadowAssemblyPath, request.Selection.ProviderPayload),
            new EventSink(eventSink),
            cancellationToken);
        return NUnitTestingMapper.ToTesting(response, NUnitGenerationPolicy.FrameworkId);
    }

    public void Cancel(Guid runId) => _inner.Cancel(runId);

    public void Dispose() => _inner.Dispose();

    public TestingGenerationRetirementDiagnostic? GetRetirementDiagnostic()
    {
#if DEBUG && NET
        if (_inner is NUnitRuntimeSessionHandle handle)
        {
            var diagnostic = handle.VerifyUnload();
            return new TestingGenerationRetirementDiagnostic(GenerationId, diagnostic.Code, diagnostic.Message);
        }
#elif DEBUG && NETFRAMEWORK
        if (_inner is NetfxNUnitSessionHandle handle)
        {
            var diagnostic = handle.CreateRetainedDiagnostic();
            return new TestingGenerationRetirementDiagnostic(GenerationId, diagnostic.Code, diagnostic.Message);
        }
#endif
        return null;
    }

    private sealed class EventSink(ITestingRuntimeEventSink sink) : INUnitRuntimeEventSink
    {
        public void Publish(NUnitRuntimeEvent runtimeEvent)
        {
            var testCase = runtimeEvent.Case is null ? null : NUnitTestingMapper.ToTesting(runtimeEvent.Case);
            sink.Publish(new TestingRuntimeEvent(
                runtimeEvent.RunId,
                runtimeEvent.Kind,
                testCase,
                runtimeEvent.Message,
                runtimeEvent.Attachment is null ? null : new TestingAttachment(
                    runtimeEvent.Attachment.Path,
                    runtimeEvent.Attachment.Name,
                    runtimeEvent.Attachment.ContentType,
                    runtimeEvent.Attachment.Base64),
                TestingCancellationState.None));
        }
    }
}
