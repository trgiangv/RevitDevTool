namespace DevTools.NUnit.TestAdapter.Tests;

public sealed class ProcessRunnerClientTests
{
    [Fact]
    public void Mtp_and_vstest_link_the_same_runner_client()
    {
        var repositoryRoot = FindRepositoryRoot();
        var shared = Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.Core",
            "Client",
            "ProcessRunnerClient.cs");
        Assert.True(File.Exists(shared));

        var client = File.ReadAllText(shared);
        Assert.Contains("internal static IReadOnlyList<string> BuildHostArguments", client);
        Assert.Contains("filter.Names", client);
        Assert.Contains("filter.FullNames", client);
        Assert.DoesNotContain("filterXml", client);
        Assert.Contains("ReadToEndAsync()", client);
        Assert.DoesNotContain("Task.Run(() => process.StandardOutput.ReadToEnd())", client);
        Assert.Contains("AddArgument(startInfo, argument)", client);
        Assert.Contains("The RevitDevTool host test run did not finish within", client);
        Assert.Contains("Timed out reading host test output.", client);
        Assert.Contains("options.DebugParentPid", client);
        Assert.DoesNotContain("NUnitRunnerCli.DiscoverCommand", client);
        Assert.DoesNotContain("IReadOnlyList<NUnitDiscoveredTest> Discover", client);

        Assert.False(File.Exists(Path.Combine(
            repositoryRoot, "source", "DevTools.NUnit.Mtp", "ProcessRunnerClient.cs")));
        Assert.False(File.Exists(Path.Combine(
            repositoryRoot, "source", "DevTools.NUnit.TestAdapter", "Runner", "ProcessRunnerClient.cs")));

        var mtp = File.ReadAllText(Path.Combine(
            repositoryRoot, "source", "DevTools.NUnit.Mtp", "DevTools.NUnit.Mtp.csproj"));
        var adapter = File.ReadAllText(Path.Combine(
            repositoryRoot, "source", "DevTools.NUnit.TestAdapter", "DevTools.NUnit.TestAdapter.csproj"));
        Assert.Contains("Core\\Client\\ProcessRunnerClient.cs", mtp);
        Assert.Contains("Core\\Client\\ProcessRunnerClient.cs", adapter);
        Assert.Contains("Core\\Client\\NUnitProcessTransportAdapter.cs", adapter);
        Assert.Contains("Core\\Client\\NUnitTestingMapping.cs", adapter);
        Assert.Contains("Testing\\ITestRunnerTransport.cs", adapter);
        Assert.Contains("DevTools.TestRunner.exe", File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "build",
            "DevTools.NUnit.TestAdapter.targets")));
        Assert.Contains("Core\\Client\\HostRunOptions.cs", mtp);
        Assert.Contains("Core\\Client\\HostRunOptions.cs", adapter);
        Assert.Contains("Core\\Client\\NUnitRunnerPaths.cs", mtp);
        Assert.Contains("Core\\Client\\NUnitRunnerPaths.cs", adapter);
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
        Assert.Contains("ToTestingHostOptions", source, StringComparison.Ordinal);
        Assert.Contains("Environment.ProcessId", source, StringComparison.Ordinal);
        Assert.Contains("ITestRunnerTransport", source, StringComparison.Ordinal);
        Assert.Contains("TestingFrameworkIds.NUnit", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetClient().Discover", source, StringComparison.Ordinal);
        Assert.Contains("LocalNUnitTestDiscoverer.Discover", source, StringComparison.Ordinal);
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
