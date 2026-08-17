using DevTools.NUnit.Core;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.Mtp;

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
            ToHostRunOptions(hostOptions),
            NUnitMtpMapping.ToRunnerFilter(request.Selection));
        var mapped = cases.Select(NUnitMtpMapping.ToTesting).ToList();
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

    static HostRunOptions ToHostRunOptions(TestingHostOptions options) =>
        new(
            options.Host,
            options.HostVersion,
            options.HostLaunch,
            options.HostTimeoutSeconds,
            options.HostLaunchTimeoutSeconds,
            options.RunnerPath,
            options.DebugParentPid);
}
