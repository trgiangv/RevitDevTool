namespace DevTools.NUnit.TestAdapter.Runner;

public static class RunnerClientFactory
{
    public static IRunnerClient Create()
    {
        var options = AdapterSettings.Current.ToRunnerHostOptions();
        return new ProcessRunnerClient(ProcessRunnerClient.ResolveRunnerPath(options));
    }
}
