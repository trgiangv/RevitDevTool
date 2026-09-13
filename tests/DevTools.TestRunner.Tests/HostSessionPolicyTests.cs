namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class HostSessionPolicyTests
{
    [TestMethod]
    public void ForceLaunch_false_reuses_matching_host_then_falls_back_to_spawn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner",
            "Services",
            "TestSession.cs"));

        Assert.Contains("reuses a matching-version instance when one is already running", source, StringComparison.Ordinal);
        Assert.Contains("otherwise starts a new host", source, StringComparison.Ordinal);
        Assert.Contains("always starts a new host", source, StringComparison.Ordinal);

        var reuseBlockStart = source.IndexOf("if (!forceLaunch)", StringComparison.Ordinal);
        var launchCall = source.IndexOf("launchService.Start", StringComparison.Ordinal);
        Assert.IsTrue(reuseBlockStart >= 0 && launchCall > reuseBlockStart);
        var reuseBlock = source[reuseBlockStart..launchCall];
        Assert.Contains("Discover(hostName, version)", reuseBlock, StringComparison.Ordinal);
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

    [TestMethod]
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

    [TestMethod]
    public void TestSession_discovery_prefers_oldest_matching_pid()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.TestRunner",
            "Services",
            "TestSession.cs"));

        Assert.Contains("OrderBy(instance => instance.ProcessId)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderByDescending", source, StringComparison.Ordinal);
    }

    [TestMethod]
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

    [TestMethod]
    public void Core_execution_coordinator_attaches_after_pipe_ensure_and_before_provider_operation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner",
            "Services",
            "TestCoordinator.cs"));
        var providerSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner",
            "RunnerCommands.cs"));

        var ensure = source.IndexOf("EnsurePipeAsync", StringComparison.Ordinal);
        var attach = source.IndexOf("debugger.TryAttach", StringComparison.Ordinal);
        var run = source.IndexOf("await operation", StringComparison.Ordinal);
        Assert.IsTrue(ensure >= 0 && attach > ensure && run > attach);
        Assert.DoesNotContain("DebugHostLifetime", source, StringComparison.Ordinal);
        Assert.Contains("context.DebugParentPid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AttachLog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryDetach", source, StringComparison.Ordinal);
        Assert.DoesNotContain("cancellationToken.Register", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldDetachHost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TesthostExited", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProcessById", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DebugAttachScope", source, StringComparison.Ordinal);
        var attachApi = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner",
            "Debugging",
            "IDebuggerAttach.cs"));
        var vsAttach = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.TestRunner",
            "Debugging",
            "VisualStudioAttach.cs"));
        Assert.DoesNotContain("TryDetach", attachApi, StringComparison.Ordinal);
        Assert.Contains("FindHost", vsAttach, StringComparison.Ordinal);
        Assert.Contains("StaWorker", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandEvents", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain("PeekMessage", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain("WaitForDebuggedHost", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain("DetachOtherDebuggees", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain(".Detach(", vsAttach, StringComparison.Ordinal);
        Assert.Contains("LocalProcesses.OfType<Process>()", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain("processes.Item(index)", vsAttach, StringComparison.Ordinal);
        Assert.Contains("GetActiveObject", vsAttach, StringComparison.Ordinal);
        Assert.Contains("SetApartmentState", vsAttach, StringComparison.Ordinal);
        Assert.Contains("OleMessageFilter.Register", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain("FindDteDebugging", vsAttach, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRunningObjectTable", vsAttach, StringComparison.Ordinal);
        var attachBlock = source[attach..run];
        Assert.Contains("new AttachTarget", attachBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("context.HostVersion", attachBlock, StringComparison.Ordinal);
        Assert.Contains("ExecuteAsync", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsurePipeAsync", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TestEventKinds.RunFinishing", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("bufferedEvents", providerSource, StringComparison.Ordinal);
        var nunitRuntime = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.Runtime",
            "NUnitRuntimeSession.cs"));
        Assert.DoesNotContain("TestEventKinds.RunFinishing", nunitRuntime, StringComparison.Ordinal);
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
