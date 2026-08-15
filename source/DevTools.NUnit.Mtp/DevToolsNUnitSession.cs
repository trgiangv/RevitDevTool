using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Mtp;

internal sealed class DevToolsNUnitSession
{
    private readonly IRunnerTransport _transport;

    internal DevToolsNUnitSession(IRunnerTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    internal IReadOnlyList<NUnitCaseResult> Run(
        string assemblyPath,
        HostRunOptions options,
        RunnerTestFilter filter) =>
        _transport.Run(RequireAssembly(assemblyPath), options, filter);

    internal void Cancel() => _transport.Cancel();

    private static string RequireAssembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Test assembly path is required.", nameof(assemblyPath));

        return Path.GetFullPath(assemblyPath);
    }
}
