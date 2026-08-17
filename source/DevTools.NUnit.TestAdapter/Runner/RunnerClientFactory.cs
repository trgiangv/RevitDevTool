using DevTools.NUnit.Core;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.TestAdapter.Runner;

internal static class RunnerClientFactory
{
    public static ITestRunnerTransport Create()
    {
        var options = AdapterSettings.Current.ToHostRunOptions();
        return new NUnitProcessTransportAdapter(
            new ProcessRunnerClient(ProcessRunnerClient.ResolveRunnerPath(options)));
    }
}
