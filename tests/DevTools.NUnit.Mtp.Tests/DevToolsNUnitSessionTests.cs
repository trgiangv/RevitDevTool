using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Mtp;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace DevTools.NUnit.Mtp.Tests;

public sealed class DevToolsNUnitSessionTests
{
    [Fact]
    public void Metadata_filter_keeps_matching_names()
    {
        var tests = new[]
        {
            new NUnitDiscoveredTest("1", "Alpha", "Fixture.Alpha"),
            new NUnitDiscoveredTest("2", "Beta", "Fixture.Beta"),
        };

        var filtered = NUnitMetadataDiscoverer.Filter(tests, ["Beta"], []);
        Assert.Equal("Fixture.Beta", Assert.Single(filtered).FullName);
    }

    [Fact]
    public void Run_returns_pass_fail_skip_and_error_from_transport()
    {
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestingRunResponse(
                Guid.NewGuid(),
                TestingFrameworkIds.NUnit,
                "gen",
                [
                    new TestingCaseResult("1", "Pass", "Passed", 10, null, null, null, null, [], []),
                    new TestingCaseResult("2", "Fail", "Failed", 20, "boom", "at Foo", null, null, [], []),
                    new TestingCaseResult("3", "Skip", "Skipped", 0, "ignored", null, null, null, [], []),
                    new TestingCaseResult("4", "Err", "Error", 5, "init", null, null, null, [], []),
                ],
                TestingCancellationState.None,
                null,
                null),
        };
        var session = new DevToolsNUnitSession(transport);

        var response = session.Run(
            "C:\\tests\\a.dll",
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe"),
            new TestingSelection([], null));

        Assert.Equal(TestingFrameworkIds.NUnit, transport.LastRequest!.FrameworkId);
        Assert.Equal(["Passed", "Failed", "Skipped", "Error"], response.Results.Select(result => result.Outcome).ToArray());
    }

    [Fact]
    public void DiscoverNodes_completes_when_runner_path_cannot_be_read()
    {
        var runnerPath = Path.Combine(Path.GetTempPath(), "devtools-mtp-locked-runner-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllBytes(runnerPath, [0x4D, 0x5A]);
        try
        {
            using (new FileStream(runnerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var nodes = DevToolsNUnitFramework.DiscoverNodes(
                    typeof(DevToolsNUnitSessionTests).Assembly.Location,
                    RunnerTestFilter.Empty);
                Assert.NotNull(nodes);
            }
        }
        finally
        {
            try { File.Delete(runnerPath); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Run_sends_nunit_framework_id_to_generic_transport()
    {
        var transport = new FakeTestRunnerTransport();
        var session = new DevToolsNUnitSession(transport);

        session.Run(
            "C:\\tests\\a.dll",
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\missing-devtools-testrunner.exe"),
            new TestingSelection(["HostSmokeTests.Arithmetic"], null));

        Assert.Equal(TestingFrameworkIds.NUnit, transport.LastRequest!.FrameworkId);
        Assert.Equal(["HostSmokeTests.Arithmetic"], transport.LastRequest.Selection.TestIds.ToArray());
    }

    [Fact]
    public void Name_filter_round_trips_through_generic_selection()
    {
        var original = RunnerTestFilter.FromNames("Arithmetic_runs_inside_host");
        var selection = NUnitTestingMapping.ToSelection(original);
        var restored = NUnitTestingMapping.ToRunnerFilter(selection);

        Assert.Empty(selection.TestIds);
        Assert.Contains("<name>Arithmetic_runs_inside_host</name>", selection.ProviderPayload, StringComparison.Ordinal);
        Assert.Equal(["Arithmetic_runs_inside_host"], restored.Names.ToArray());
        Assert.Empty(restored.FullNames);
    }
}

public sealed class ProcessRunnerClientTests
{
    [Fact]
    public void BuildHostArguments_sends_name_and_test_tokens()
    {
        var options = new HostRunOptions("Revit", "2026", true, 60, 180, @"C:\Runner.exe");
        var args = ProcessRunnerClient.BuildHostArguments(
            @"C:\tests\HostTests.dll",
            options,
            new RunnerTestFilter(["Arithmetic_runs_inside_host"], ["HostSmokeTests.Arithmetic"]));

        Assert.Equal(
            [
                "run",
                @"C:\tests\HostTests.dll",
                "--host",
                "Revit",
                "--host-version",
                "2026",
                "--host-timeout",
                "60",
                "--host-launch-timeout",
                "180",
                "--host-launch",
                "--name",
                """["Arithmetic_runs_inside_host"]""",
                "--test",
                """["HostSmokeTests.Arithmetic"]""",
            ],
            args);
    }

    [Fact]
    public void BuildHostArguments_run_adds_debug_flags_when_parent_pid_is_set()
    {
        var options = new HostRunOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe", DebugParentPid: 4242);
        var args = ProcessRunnerClient.BuildHostArguments(
            @"C:\tests\HostTests.dll",
            options,
            RunnerTestFilter.Empty);

        Assert.DoesNotContain("--debug", args);
        Assert.Contains("--debug-parent-pid", args);
        Assert.Contains("4242", args);
    }

    [Fact]
    public void BuildHostArguments_omits_debug_flags_when_parent_pid_is_absent()
    {
        var options = new HostRunOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe");
        var args = ProcessRunnerClient.BuildHostArguments(
            @"C:\tests\HostTests.dll",
            options,
            RunnerTestFilter.Empty);

        Assert.DoesNotContain("--debug", args);
        Assert.DoesNotContain("--debug-parent-pid", args);
        Assert.DoesNotContain("4242", args);
    }
}

public sealed class HostOptionsLoaderTests
{
    [Fact]
    public void Load_reads_generated_host_json()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(
            Path.Combine(directory.Path, HostOptionsLoader.OptionsFileName),
            """
            {
              "host": "Civil3D",
              "hostVersion": "2026",
              "hostLaunch": true,
              "hostTimeoutSeconds": 90,
              "hostLaunchTimeoutSeconds": 240,
              "runnerPath": "C:\\Runner.exe"
            }
            """);

        var options = HostOptionsLoader.Load(directory.Path);

        Assert.Equal("Civil3D", options.Host);
        Assert.Equal("2026", options.HostVersion);
        Assert.True(options.HostLaunch);
        Assert.Equal(90, options.HostTimeoutSeconds);
        Assert.Equal(240, options.HostLaunchTimeoutSeconds);
        Assert.Equal(@"C:\Runner.exe", options.RunnerPath);
    }

    [Fact]
    public void Load_throws_when_host_json_is_missing()
    {
        using var directory = new TempDirectory();
        var ex = Assert.Throws<InvalidOperationException>(() => HostOptionsLoader.Load(directory.Path));
        Assert.Contains("devtools.nunit.host.json", ex.Message, StringComparison.Ordinal);
    }
}

public sealed class TestNodeMapperTests
{
    [Fact]
    public void ToDiscoveredNode_uses_fullname_as_stable_uid()
    {
        var node = DevToolsNUnitFramework.ToDiscoveredNode(
            new NUnitDiscoveredTest("id-1", "Arithmetic", "HostSmokeTests.Arithmetic"));

        Assert.Equal("HostSmokeTests.Arithmetic", node.Uid.Value);
        Assert.Equal("Arithmetic", node.DisplayName);
        Assert.NotNull(node.Properties.SingleOrDefault<DiscoveredTestNodeStateProperty>());
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("HostSmokeTests", identity.TypeName);
        Assert.Equal("Arithmetic", identity.MethodName);
    }

    [Theory]
    [InlineData("Passed", typeof(PassedTestNodeStateProperty))]
    [InlineData("Failed", typeof(FailedTestNodeStateProperty))]
    [InlineData("Skipped", typeof(SkippedTestNodeStateProperty))]
    [InlineData("Error", typeof(ErrorTestNodeStateProperty))]
    public void ToResultNode_maps_outcomes(string outcome, Type stateType)
    {
        var node = DevToolsNUnitFramework.ToResultNode(
            new NUnitCaseResult("id", "Case", outcome, 12, "msg", null, null, SkipReason: "ignored"));

        Assert.Equal("Case", node.DisplayName);
        Assert.Equal("id", node.Uid.Value);
        Assert.Contains(node.Properties.AsEnumerable(), property => property.GetType() == stateType);
    }

    [Fact]
    public void ToResultNode_uses_fullname_as_stable_uid()
    {
        var node = DevToolsNUnitFramework.ToResultNode(
            new NUnitCaseResult(
                "HostSmokeTests/Arithmetic#0",
                "Arithmetic",
                "Passed",
                12,
                null,
                null,
                null,
                FullName: "HostSmokeTests.Arithmetic"));

        Assert.Equal("HostSmokeTests.Arithmetic", node.Uid.Value);
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("HostSmokeTests", identity.TypeName);
        Assert.Equal("Arithmetic", identity.MethodName);
    }

    [Fact]
    public void ToResultNode_maps_standard_output()
    {
        var node = DevToolsNUnitFramework.ToResultNode(
            new NUnitCaseResult(
                "id",
                "Writes_output",
                "Passed",
                12,
                null,
                null,
                "ERR devtools-nunit-sample-trace\ndevtools-nunit-sample-debug"));

        var stdout = node.Properties.Single<StandardOutputProperty>();
        Assert.Contains("devtools-nunit-sample-trace", stdout.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("devtools-nunit-sample-debug", stdout.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ToRunnerFilter_prefers_selected_uids()
    {
        var filter = new TestNodeUidListFilter([new TestNodeUid("HostSmokeTests.Arithmetic")]);
        var selection = DevToolsNUnitFramework.ToRunnerFilter(filter, "Intentional_failure_for_demo");
        Assert.Equal(["HostSmokeTests.Arithmetic"], selection.FullNames.ToArray());
        Assert.Empty(selection.Names);
    }

    [Fact]
    public void ToRunnerFilter_uses_method_name_when_no_uid_list()
    {
        var selection = DevToolsNUnitFramework.ToRunnerFilter(null, nameFilter: "Arithmetic_runs_inside_host");
        Assert.Equal(["Arithmetic_runs_inside_host"], selection.Names.ToArray());
        Assert.Empty(selection.FullNames);
    }
}

internal sealed class FakeTestRunnerTransport : ITestRunnerTransport
{
    internal TestingRunRequest? LastRequest { get; private set; }

    internal TestingHostOptions? LastHostOptions { get; private set; }

    internal bool Cancelled { get; private set; }

    internal TestingRunResponse? Response { get; set; }

    public TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingCaseResult> onResult)
    {
        LastRequest = request;
        LastHostOptions = hostOptions;
        var response = Response ?? new TestingRunResponse(
            request.RunId,
            request.FrameworkId,
            null,
            [],
            TestingCancellationState.None,
            null,
            null);
        foreach (var result in response.Results)
            onResult(result);
        return response;
    }

    public void Cancel(Guid runId) => Cancelled = true;

    public void Dispose()
    {
    }
}

internal sealed class FakeRunnerTransport : IRunnerTransport
{
    internal string? LastAssemblyPath { get; private set; }

    internal RunnerTestFilter LastFilter { get; private set; }

    internal bool Cancelled { get; private set; }

    internal IReadOnlyList<NUnitCaseResult> Results { get; set; } = [];

    public IReadOnlyList<NUnitCaseResult> Run(
        string assemblyPath,
        HostRunOptions options,
        RunnerTestFilter filter)
    {
        LastAssemblyPath = assemblyPath;
        LastFilter = filter;
        return Results;
    }

    public void Cancel() => Cancelled = true;
}

internal sealed class TempDirectory : IDisposable
{
    internal TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "devtools-nunit-mtp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
