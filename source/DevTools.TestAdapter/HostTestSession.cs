using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.TestAdapter;

internal sealed class HostTestSession
{
    readonly ITestRunnerTransport _transport;
    Guid _runId;

    internal HostTestSession(ITestRunnerTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    internal TestingRunResponse Run(
        string assemblyPath,
        TestingHostOptions hostOptions,
        TestingSelection selection)
    {
        _runId = Guid.NewGuid();
        var frameworkId = string.IsNullOrWhiteSpace(hostOptions.FrameworkId)
            ? HostOptionsLoader.DefaultFrameworkId
            : hostOptions.FrameworkId!.Trim();
        var request = new TestingRunRequest(
            TestingProtocol.CurrentVersion,
            _runId,
            frameworkId,
            new TestingAssemblyReference(RequireAssembly(assemblyPath), null, null),
            selection,
            new Dictionary<string, string>());

        return _transport.Run(request, hostOptions, _ => { });
    }

    internal void Cancel() => _transport.Cancel(_runId);

    internal void Dispose() => _transport.Dispose();

    static string RequireAssembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Test assembly path is required.", nameof(assemblyPath));

        return Path.GetFullPath(assemblyPath);
    }
}
