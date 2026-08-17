using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Mtp;

public sealed class TestingRunnerSession : IDisposable
{
    readonly ITestRunnerTransport _transport;

    public TestingRunnerSession(ITestRunnerTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingCaseResult>? onResult = null)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (hostOptions is null)
            throw new ArgumentNullException(nameof(hostOptions));

        return _transport.Run(request, hostOptions, onResult ?? (_ => { }));
    }

    public void Cancel(Guid runId) => _transport.Cancel(runId);

    public void Dispose() => _transport.Dispose();
}
