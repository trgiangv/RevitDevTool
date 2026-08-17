using DevTools.NUnit.Core;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.TestAdapter.Runner;

internal static class RunnerClientFactory
{
    public static ITestRunnerTransport Create()
    {
        var options = AdapterSettings.Current;
        return new ProcessTestRunnerClient(NUnitRunnerPaths.ResolveRunnerPath(options.RunnerPath));
    }
}
