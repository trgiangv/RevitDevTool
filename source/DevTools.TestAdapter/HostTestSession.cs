using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.TestAdapter;

internal sealed class HostTestSession
{
    private readonly ITestRunnerTransport _transport;
    private Guid _runId;

    internal HostTestSession(ITestRunnerTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    internal TestingRunResponse Run(
        string assemblyPath,
        TestingHostOptions hostOptions,
        TestingSelection selection,
        Action<TestingEvent>? onEvent = null)
    {
        _runId = Guid.NewGuid();
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        if (string.IsNullOrWhiteSpace(hostOptions.FrameworkId))
            throw new InvalidOperationException(
                "RevitDevTool.TestAdapter requires 'devtools.frameworkId' in testconfig.json.");
        var frameworkId = hostOptions.FrameworkId!.Trim();
        var request = new TestingRunRequest(
            TestingProtocol.CurrentVersion,
            _runId,
            frameworkId,
            new TestingAssemblyReference(Path.GetFullPath(assemblyPath)),
            selection);

        var wireHost = hostOptions with { FrameworkId = null, RunnerPath = null };
        return _transport.Run(request, wireHost, onEvent ?? (_ => { }));
    }

    internal void Cancel() => _transport.Cancel(_runId);

    internal void Dispose() => _transport.Dispose();
}
