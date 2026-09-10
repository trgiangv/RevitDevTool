using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.TestAdapter;

internal sealed class TestRunSession
{
    private readonly ITestRunnerTransport _transport;
    private Guid _runId;

    internal TestRunSession(ITestRunnerTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    internal TestRunResponse Run(
        string assemblyPath,
        TestHostOptions hostOptions,
        TestFrameworkId frameworkId,
        TestSelection selection,
        Action<TestEvent>? onEvent = null)
    {
        _runId = Guid.NewGuid();
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        var request = new TestRunRequest(
            TestingProtocol.CurrentVersion,
            _runId,
            frameworkId,
            new TestAssemblyReference(Path.GetFullPath(assemblyPath)),
            selection);

        return _transport.Run(request, hostOptions, onEvent ?? (_ => { }));
    }

    internal void Cancel() => _transport.Cancel(_runId);

    internal void Dispose() => _transport.Dispose();
}
