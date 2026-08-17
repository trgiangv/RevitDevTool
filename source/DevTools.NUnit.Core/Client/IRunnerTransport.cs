using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Core;

/// <summary>
/// Spawns <c>DevTools.TestRunner</c> for <c>run</c> only. Shared by MTP and VSTest.
/// Discovery is local PE metadata, not this transport.
/// </summary>
public interface IRunnerTransport
{
    IReadOnlyList<NUnitCaseResult> Run(
        string assemblyPath,
        HostRunOptions options,
        RunnerTestFilter filter);

    void Cancel();
}
