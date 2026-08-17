using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.Core;

/// <summary>
/// Adapts the NUnit-JSON TestRunner client onto <see cref="ITestRunnerTransport"/>
/// until the live Runner emits <c>testing/*</c> envelopes.
/// </summary>
internal sealed class NUnitProcessTransportAdapter : ITestRunnerTransport
{
    readonly ProcessRunnerClient _client;

    internal NUnitProcessTransportAdapter(ProcessRunnerClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingCaseResult> onResult)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (onResult is null)
            throw new ArgumentNullException(nameof(onResult));

        var cases = _client.Run(
            request.Assembly.Path,
            NUnitTestingMapping.ToHostRunOptions(hostOptions),
            NUnitTestingMapping.ToRunnerFilter(request.Selection));
        var mapped = cases.Select(NUnitTestingMapping.ToTesting).ToList();
        foreach (var result in mapped)
            onResult(result);

        return new TestingRunResponse(
            request.RunId,
            TestingFrameworkIds.NUnit,
            GenerationId: null,
            mapped,
            TestingCancellationState.None,
            null,
            null);
    }

    public void Cancel(Guid runId) => _client.Cancel();

    public void Dispose() => _client.Dispose();
}
