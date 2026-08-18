using DevTools.TestAdapter;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace DevTools.TestAdapter.Tests;

public sealed class HostTestSessionTests
{
    [Fact]
    public void ScaleForRun_multiplies_per_test_timeout()
    {
        var options = new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe");
        var scaled = HostTestFramework.ScaleForRun(options, testCount: 3);
        Assert.Equal(180, scaled.PerTestTimeoutSeconds);
        Assert.Equal(60, options.PerTestTimeoutSeconds);
    }

    [Fact]
    public void Metadata_filter_keeps_matching_names()
    {
        var tests = new[]
        {
            new TestingDiscoveredTest("1", "Alpha", "Fixture.Alpha"),
            new TestingDiscoveredTest("2", "Beta", "Fixture.Beta"),
        };

        var filtered = MetadataTestDiscoverer.Filter(tests, ["Beta"], []);
        Assert.Equal("Fixture.Beta", Assert.Single(filtered).FullName);
    }

    [Fact]
    public void Run_returns_pass_fail_skip_and_error_from_transport()
    {
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestingRunResponse(
                Guid.NewGuid(),
                "nunit",
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
        var session = new HostTestSession(transport);

        var response = session.Run(
            "C:\\tests\\a.dll",
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe"),
            new TestingSelection([], null));

        Assert.Equal("nunit", transport.LastRequest!.FrameworkId);
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
                var nodes = HostTestFramework.DiscoverNodes(
                    typeof(HostTestSessionTests).Assembly.Location,
                    new TestingSelection([], null),
                    ["FactAttribute"]);
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
        var session = new HostTestSession(transport);

        session.Run(
            "C:\\tests\\a.dll",
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\missing-devtools-testrunner.exe"),
            new TestingSelection(["HostSmokeTests.Arithmetic"], null));

        Assert.Equal(HostOptionsLoader.DefaultFrameworkId, transport.LastRequest!.FrameworkId);
        Assert.Equal(["HostSmokeTests.Arithmetic"], transport.LastRequest.Selection.TestIds.ToArray());
    }

    [Fact]
    public void Name_filter_round_trips_through_generic_selection()
    {
        var selection = HostTestFramework.ToRunnerFilter(null, nameFilter: "Arithmetic_runs_inside_host");

        Assert.Empty(selection.TestIds);
        Assert.Equal(["Arithmetic_runs_inside_host"], selection.Names!.ToArray());
        Assert.Null(selection.ProviderPayload);
    }
}

public sealed class ProcessTestRunnerCliTests
{
    [Fact]
    public void BuildRunArguments_sends_framework_and_test_tokens()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                "nunit",
                new TestingAssemblyReference(@"C:\tests\HostTests.dll", null, null),
                new TestingSelection(["HostSmokeTests.Arithmetic"]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2026", true, 60, 180, @"C:\Runner.exe"));

        Assert.Equal("run", args[0]);
        Assert.Contains("--framework", args);
        Assert.Contains("nunit", args);
        Assert.Contains(@"C:\tests\HostTests.dll", args);
        Assert.Contains("--host", args);
        Assert.Contains("Revit", args);
        Assert.Contains("--force-launch", args);
        Assert.Contains("--test", args);
        Assert.Contains("""["HostSmokeTests.Arithmetic"]""", args);
        Assert.DoesNotContain("--name", args);
        Assert.DoesNotContain("discover", args);
    }

    [Fact]
    public void BuildRunArguments_run_adds_debug_flags_when_parent_pid_is_set()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                "nunit",
                new TestingAssemblyReference(@"C:\tests\HostTests.dll", null, null),
                new TestingSelection([]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe", DebugParentPid: 4242));

        Assert.DoesNotContain("--debug", args);
        Assert.Contains("--debug-parent-pid", args);
        Assert.Contains("4242", args);
    }

    [Fact]
    public void BuildRunArguments_omits_debug_flags_when_parent_pid_is_absent()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                "nunit",
                new TestingAssemblyReference(@"C:\tests\HostTests.dll", null, null),
                new TestingSelection([]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe"));

        Assert.DoesNotContain("--debug", args);
        Assert.DoesNotContain("--debug-parent-pid", args);
        Assert.DoesNotContain("4242", args);
    }
}

public sealed class HostOptionsLoaderTests
{
    [Fact]
    public void Load_reads_mtp_iconfiguration_keys()
    {
        IConfiguration configuration = new StubConfiguration(new Dictionary<string, string?>
        {
            [HostOptionsLoader.Keys.Configuration(HostOptionsLoader.Keys.HostName)] = "Civil3D",
            [HostOptionsLoader.Keys.Configuration(HostOptionsLoader.Keys.HostVersion)] = "2026",
            [HostOptionsLoader.Keys.Configuration(HostOptionsLoader.Keys.ForceLaunch)] = "true",
            [HostOptionsLoader.Keys.Configuration(HostOptionsLoader.Keys.PerTestTimeoutSeconds)] = "90",
            [HostOptionsLoader.Keys.Configuration(HostOptionsLoader.Keys.LaunchTimeoutSeconds)] = "240",
            [HostOptionsLoader.Keys.Configuration(HostOptionsLoader.Keys.RunnerPath)] = @"C:\Runner.exe",
            [HostOptionsLoader.Keys.Configuration(HostOptionsLoader.Keys.FrameworkId)] = HostOptionsLoader.DefaultFrameworkId,
            [HostOptionsLoader.Keys.DiscoveryAttribute(0)] = "TestAttribute",
            [HostOptionsLoader.Keys.DiscoveryAttribute(1)] = "TheoryAttribute",
        });

        var options = HostOptionsLoader.Load(configuration);

        Assert.Equal("Civil3D", options.HostName);
        Assert.Equal("2026", options.HostVersion);
        Assert.True(options.ForceLaunch);
        Assert.Equal(90, options.PerTestTimeoutSeconds);
        Assert.Equal(240, options.LaunchTimeoutSeconds);
        Assert.Equal(HostOptionsLoader.DefaultFrameworkId, options.FrameworkId);
        Assert.Equal(["TestAttribute", "TheoryAttribute"], options.DiscoveryAttributes);
        Assert.Equal(@"C:\Runner.exe", options.RunnerPath);
    }

    [Fact]
    public void Load_throws_when_devtools_section_is_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => HostOptionsLoader.Load(new StubConfiguration(new Dictionary<string, string?>())));
        Assert.Contains(HostOptionsLoader.ConfigFileName, ex.Message, StringComparison.Ordinal);
        Assert.Contains(HostOptionsLoader.ConfigSectionName, ex.Message, StringComparison.Ordinal);
    }
}

public sealed class TestNodeMapperTests
{
    [Fact]
    public void CreateErrorNode_sets_error_state()
    {
        var node = TestNodeProperties.CreateErrorNode("uid", "display", new InvalidOperationException("boom"));
        Assert.Equal("uid", node.Uid.Value);
        Assert.Equal("display", node.DisplayName);
        Assert.NotNull(node.Properties.SingleOrDefault<ErrorTestNodeStateProperty>());
    }

    [Fact]
    public void ToDiscoveredNode_uses_fullname_as_stable_uid()
    {
        var node = HostTestFramework.ToDiscoveredNode(
            new TestingDiscoveredTest("id-1", "Arithmetic", "HostSmokeTests.Arithmetic"));

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
        var node = HostTestFramework.ToResultNode(
            new TestingCaseResult("id", "Case", outcome, 12, "msg", null, null, null, [], [], SkipReason: "ignored"));

        Assert.Equal("Case", node.DisplayName);
        Assert.Equal("id", node.Uid.Value);
        Assert.Contains(node.Properties.AsEnumerable(), property => property.GetType() == stateType);
    }

    [Fact]
    public void ToResultNode_uses_fullname_as_stable_uid()
    {
        var node = HostTestFramework.ToResultNode(
            new TestingCaseResult(
                "HostSmokeTests/Arithmetic#0",
                "Arithmetic",
                "Passed",
                12,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: "HostSmokeTests.Arithmetic"));

        Assert.Equal("HostSmokeTests.Arithmetic", node.Uid.Value);
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("HostSmokeTests", identity.TypeName);
        Assert.Equal("Arithmetic", identity.MethodName);
    }

    [Fact]
    public void ToResultNode_maps_standard_output()
    {
        var node = HostTestFramework.ToResultNode(
            new TestingCaseResult(
                "id",
                "Writes_output",
                "Passed",
                12,
                null,
                null,
                "ERR devtools-nunit-sample-trace\ndevtools-nunit-sample-debug",
                null,
                [],
                []));

        var stdout = node.Properties.Single<StandardOutputProperty>();
        Assert.Contains("devtools-nunit-sample-trace", stdout.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("devtools-nunit-sample-debug", stdout.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ToRunnerFilter_prefers_selected_uids()
    {
        var filter = new TestNodeUidListFilter([new TestNodeUid("HostSmokeTests.Arithmetic")]);
        var selection = HostTestFramework.ToRunnerFilter(filter, "Intentional_failure_for_demo");
        Assert.Equal(["HostSmokeTests.Arithmetic"], selection.TestIds.ToArray());
        Assert.Null(selection.ProviderPayload);
    }

    [Fact]
    public void ToRunnerFilter_uses_method_name_when_no_uid_list()
    {
        var selection = HostTestFramework.ToRunnerFilter(null, nameFilter: "Arithmetic_runs_inside_host");
        Assert.Equal(["Arithmetic_runs_inside_host"], selection.Names!.ToArray());
        Assert.Empty(selection.TestIds);
        Assert.Null(selection.ProviderPayload);
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

internal sealed class StubConfiguration : IConfiguration
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    internal StubConfiguration(IReadOnlyDictionary<string, string?> values) => _values = values;

    public string? this[string key] =>
        _values.TryGetValue(key, out var value) ? value : null;
}
