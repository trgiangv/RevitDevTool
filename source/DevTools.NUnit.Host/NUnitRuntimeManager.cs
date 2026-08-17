using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Results;
using DevTools.NUnit.Transport.Runtime;
using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;
using Microsoft.Extensions.Logging;

namespace DevTools.NUnit.Host;

/// <summary>
/// NUnit protocol adapter over the framework-neutral generation/session kernel.
/// Discovery deliberately remains on the NUnit compatibility contract.
/// </summary>
public sealed class NUnitRuntimeManager : IDisposable
{
    private readonly TestingGenerationStore _generations;
    private readonly NUnitGenerationPolicy _policy;
    private readonly INUnitRuntimeSessionFactory _discoveryFactory;
    private readonly TestingRuntimeSessionManager _sessions;

    public NUnitRuntimeManager(
        NUnitGenerationBuilder generationBuilder,
        INUnitRuntimeSessionFactory sessionFactory,
        ILogger<NUnitRuntimeManager>? logger = null)
        : this(generationBuilder?.Store ?? throw new ArgumentNullException(nameof(generationBuilder)),
            generationBuilder.Policy,
            sessionFactory)
    {
    }

    internal NUnitRuntimeManager(
        TestingGenerationStore generations,
        NUnitGenerationPolicy policy,
        INUnitRuntimeSessionFactory sessionFactory)
    {
        _generations = generations ?? throw new ArgumentNullException(nameof(generations));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _discoveryFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _sessions = new TestingRuntimeSessionManager(
            _generations,
            _policy,
            new NUnitTestingRuntimeSessionFactory(_discoveryFactory));
    }

    public bool IsOperationActive => _sessions.IsOperationActive;

    public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var sourceAssemblyPath = ResolveAssembly(request.AssemblyPath);
        var generation = BuildGeneration(sourceAssemblyPath);
        using var session = _discoveryFactory.Create(NUnitGenerationManifestAdapter.ToNUnit(generation));
        var response = session.Discover(new NUnitDiscoverRequest(generation.ShadowAssemblyPath, request.Filter));
        return response with { GenerationId = string.IsNullOrWhiteSpace(response.GenerationId) ? generation.GenerationId : response.GenerationId };
    }

    public NUnitRunResponse Run(
        NUnitRunRequest request,
        Action<NUnitProgressEvent> publish,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (publish is null)
            throw new ArgumentNullException(nameof(publish));

        var sourceAssemblyPath = ResolveAssembly(request.AssemblyPath);
        var testingRequest = new TestingRunRequest(
            ProtocolVersion: 1,
            request.RunId,
            NUnitGenerationPolicy.FrameworkId,
            new TestingAssemblyReference(sourceAssemblyPath, null, null),
            new TestingSelection([], request.Filter),
            new Dictionary<string, string>());

        try
        {
            var response = _sessions.Run(testingRequest, new ProgressSink(request.RunId, publish), cancellationToken);
            return ToNUnit(response);
        }
        catch (TestingGenerationBuildException ex)
        {
            throw MapGenerationBuildException(sourceAssemblyPath, ex);
        }
    }

    public void Cancel(Guid runId) => _sessions.Cancel(runId);

    public void Dispose()
    {
        _sessions.Dispose();
#if NETFRAMEWORK
        if (_discoveryFactory is IDisposable disposableFactory)
            disposableFactory.Dispose();
#endif
    }

    private TestingGenerationManifest BuildGeneration(string sourceAssemblyPath)
    {
        try
        {
            return _generations.Build(_policy, sourceAssemblyPath);
        }
        catch (TestingGenerationBuildException ex)
        {
            throw MapGenerationBuildException(sourceAssemblyPath, ex);
        }
    }

    private static string ResolveAssembly(string assemblyPath)
    {
        var sourceAssemblyPath = NUnitAssemblyLoader.ResolveAssemblyPath(assemblyPath);
        NUnitAssemblyLoader.EnsureLoadable(sourceAssemblyPath);
        return sourceAssemblyPath;
    }

    private static NUnitAssemblyLoadException MapGenerationBuildException(string assemblyPath, Exception ex) =>
        new(NUnitAssemblyPreflightResult.Failed(assemblyPath, ex.Message, ex.ToString()));

    private static NUnitRunResponse ToNUnit(TestingRunResponse response)
    {
        var cases = response.Results.Select(result => new NUnitCaseResult(
            result.TestId,
            result.DisplayName,
            result.Outcome,
            result.DurationMilliseconds,
            result.Message,
            result.StackTrace,
            result.Output,
            result.ParentTestId,
            result.Traits.Select(trait => new NUnitTrait(trait.Name, trait.Value)).ToList(),
            result.Source is null ? null : new NUnitSourceLocation(result.Source.File, result.Source.Line),
            result.SkipReason,
            result.Attachments.Select(attachment => new NUnitAttachment(
                attachment.Description ?? string.Empty,
                attachment.ContentType,
                attachment.Path,
                attachment.Base64)).ToList(),
            result.FullName)).ToList();
        var summary = new NUnitRunSummary(
            cases.Count(testCase => string.Equals(testCase.Outcome, NUnitOutcomes.Passed, StringComparison.Ordinal)),
            cases.Count(testCase => string.Equals(testCase.Outcome, NUnitOutcomes.Failed, StringComparison.Ordinal)),
            cases.Count(testCase => string.Equals(testCase.Outcome, NUnitOutcomes.Skipped, StringComparison.Ordinal)),
            cases.Count(testCase => string.Equals(testCase.Outcome, NUnitOutcomes.Inconclusive, StringComparison.Ordinal)),
            cases.Count(testCase => string.Equals(testCase.Outcome, NUnitOutcomes.Error, StringComparison.Ordinal)),
            cases.Count(testCase => string.Equals(testCase.Outcome, NUnitOutcomes.Cancelled, StringComparison.Ordinal)));
        var diagnostic = response.DiagnosticCode is null ? null : new NUnitRuntimeDiagnostic(response.DiagnosticCode, response.DiagnosticMessage ?? string.Empty);
        return new NUnitRunResponse(response.RunId, summary, cases, response.GenerationId, diagnostic);
    }

    private sealed class ProgressSink(Guid runId, Action<NUnitProgressEvent> publish) : ITestingRuntimeEventSink
    {
        public void Publish(TestingRuntimeEvent testingEvent)
        {
            if (testingEvent.RunId != runId || testingEvent.Case is null
                || !string.Equals(testingEvent.Kind, "case.finished", StringComparison.Ordinal))
                return;

            var testCase = testingEvent.Case;
            publish(new NUnitProgressEvent(runId, new NUnitCaseResult(
                testCase.TestId,
                testCase.DisplayName,
                testCase.Outcome,
                testCase.DurationMilliseconds,
                testCase.Message,
                testCase.StackTrace,
                testCase.Output,
                testCase.ParentTestId,
                testCase.Traits.Select(trait => new NUnitTrait(trait.Name, trait.Value)).ToList(),
                testCase.Source is null ? null : new NUnitSourceLocation(testCase.Source.File, testCase.Source.Line),
                testCase.SkipReason,
                testCase.Attachments.Select(attachment => new NUnitAttachment(
                    attachment.Description ?? string.Empty,
                    attachment.ContentType,
                    attachment.Path,
                    attachment.Base64)).ToList(),
                testCase.FullName)));
        }
    }
}
