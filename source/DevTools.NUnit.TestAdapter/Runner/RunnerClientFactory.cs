using DevTools.NUnit.Core;

namespace DevTools.NUnit.TestAdapter.Runner;

internal static class RunnerClientFactory
{
    public static ProcessRunnerClient Create()
    {
        var options = AdapterSettings.Current.ToHostRunOptions();
        return new ProcessRunnerClient(ProcessRunnerClient.ResolveRunnerPath(options));
    }
}
