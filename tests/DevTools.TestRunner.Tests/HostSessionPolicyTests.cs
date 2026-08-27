namespace DevTools.TestRunner.Tests;

public sealed class HostSessionPolicyTests
{
    [Fact]
    public void ForceLaunch_false_reuses_matching_host_then_falls_back_to_spawn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner.Core",
            "Services",
            "TestSession.cs"));

        Assert.Contains("reuses a matching-version instance when one is already running", source, StringComparison.Ordinal);
        Assert.Contains("otherwise starts a new host", source, StringComparison.Ordinal);
        Assert.Contains("always starts a new host", source, StringComparison.Ordinal);

        var reuseBlockStart = source.IndexOf("if (!forceLaunch)", StringComparison.Ordinal);
        var launchCall = source.IndexOf("launchService.Start", StringComparison.Ordinal);
        Assert.True(reuseBlockStart >= 0 && launchCall > reuseBlockStart);
        var reuseBlock = source[reuseBlockStart..launchCall];
        Assert.Contains("HostLocator.Discover", reuseBlock, StringComparison.Ordinal);
        Assert.Contains("return existing", reuseBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new InvalidOperationException", reuseBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("launchService.Start", reuseBlock, StringComparison.Ordinal);
        Assert.Contains("FilePath: null", source, StringComparison.Ordinal);
        Assert.Contains("HostLaunchWaiter.UntilAsync", source, StringComparison.Ordinal);
        Assert.Contains("HostLaunchWaiter.TerminateIfIncomplete", source, StringComparison.Ordinal);
        Assert.Contains("Does not kill a reused session", source, StringComparison.Ordinal);
        Assert.DoesNotContain("languageCode: \"ENU\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new HostLaunchService()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TestRunner_run_does_not_discover_or_locate_a_host_itself()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.TestRunner",
            "RunnerCommands.cs"));

        Assert.Contains("ExecuteAsync", source, StringComparison.Ordinal);
        Assert.Contains("TestPipeClient.ConnectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataTestDiscoverer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[Command(\"discover\")]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsurePipeAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HostLocator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("launchService.Start", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HostLocator_prefers_oldest_matching_pid()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.TestRunner.Core",
            "Services",
            "HostLocator.cs"));

        Assert.Contains("OrderBy(instance => instance.ProcessId)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderByDescending", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HostLaunch_starts_the_host_exe_as_a_direct_child()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.Hosting",
            "HostLaunchService.cs"));

        Assert.Contains("UseShellExecute = false", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseShellExecute = true", source, StringComparison.Ordinal);
        Assert.Contains("RedirectStandardOutput = true", source, StringComparison.Ordinal);
        Assert.Contains("StandardInput.Close()", source, StringComparison.Ordinal);
        Assert.Contains("StdioInheritance.Suppress()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_execution_coordinator_attaches_after_pipe_ensure_and_before_provider_operation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner.Core",
            "Services",
            "ExecutionCoordinator.cs"));
        var providerSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner",
            "RunnerCommands.cs"));

        var ensure = source.IndexOf("EnsurePipeAsync", StringComparison.Ordinal);
        var attach = source.IndexOf("DebugAttachScope.TryBegin", StringComparison.Ordinal);
        var run = source.IndexOf("await operation", StringComparison.Ordinal);
        Assert.True(ensure >= 0 && attach > ensure && run > attach);
        Assert.Contains("DebugHostLifetime.Link", source, StringComparison.Ordinal);
        Assert.Contains("context.DebugParentPid", source, StringComparison.Ordinal);
        var attachBlock = source[attach..run];
        Assert.Contains("new AttachTarget", attachBlock, StringComparison.Ordinal);
        Assert.Contains("context.AssemblyPath", attachBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("context.HostVersion", attachBlock, StringComparison.Ordinal);
        Assert.Contains("ExecuteAsync", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsurePipeAsync", providerSource, StringComparison.Ordinal);
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
