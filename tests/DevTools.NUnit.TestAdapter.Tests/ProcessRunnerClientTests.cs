namespace DevTools.NUnit.TestAdapter.Tests;

public sealed class ProcessRunnerClientTests
{
    [Fact]
    public void Process_runner_client_matches_mtp_cli_and_io_patterns()
    {
        var repositoryRoot = FindRepositoryRoot();
        var client = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "Runner",
            "ProcessRunnerClient.cs"));
        var mtp = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "ProcessRunnerClient.cs"));

        Assert.Contains("internal static IReadOnlyList<string> BuildHostArguments", client);
        Assert.Contains("filter.Names", client);
        Assert.Contains("filter.FullNames", client);
        Assert.DoesNotContain("filterXml", client);
        Assert.Contains("ReadToEndAsync()", client);
        Assert.DoesNotContain("Task.Run(() => process.StandardOutput.ReadToEnd())", client);
        Assert.Contains("AddArgument(startInfo, argument)", client);
        Assert.Contains("The RevitDevTool host test run did not finish within", client);
        Assert.Contains("Timed out reading host test output.", client);

        Assert.Contains("filter.Names", mtp);
        Assert.Contains("filter.FullNames", mtp);
        Assert.Contains("ReadToEndAsync()", mtp);
        Assert.Contains("AddArgument(startInfo, argument)", mtp);
        Assert.Contains("options.DebugParentPid", client);
        Assert.Contains("command == NUnitRunnerCli.RunCommand ? options.DebugParentPid", client);
    }

    [Fact]
    public void Executor_passes_debug_flags_when_run_context_is_being_debugged()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.NUnit.TestAdapter",
            "DevToolsNUnitExecutor.cs"));

        Assert.Contains("runContext.IsBeingDebugged", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug = true", source, StringComparison.Ordinal);
        Assert.Contains("DebugParentPid = Environment.ProcessId", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
