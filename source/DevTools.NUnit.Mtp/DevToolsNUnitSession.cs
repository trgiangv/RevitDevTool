using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Mtp;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.Mtp;

internal sealed class DevToolsNUnitSession
{
    readonly TestingRunnerSession _session;

    internal DevToolsNUnitSession(ITestRunnerTransport transport)
    {
        _session = new TestingRunnerSession(transport);
    }

    internal TestingRunResponse Run(
        string assemblyPath,
        TestingHostOptions hostOptions,
        TestingSelection selection)
    {
        var request = new TestingRunRequest(
            TestingProtocol.CurrentVersion,
            Guid.NewGuid(),
            TestingFrameworkIds.NUnit,
            new TestingAssemblyReference(RequireAssembly(assemblyPath), null, null),
            selection,
            new Dictionary<string, string>());

        return _session.Run(request, hostOptions);
    }

    internal void Cancel() => _session.Cancel(Guid.Empty);

    internal void Dispose() => _session.Dispose();

    static string RequireAssembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Test assembly path is required.", nameof(assemblyPath));

        return Path.GetFullPath(assemblyPath);
    }
}
