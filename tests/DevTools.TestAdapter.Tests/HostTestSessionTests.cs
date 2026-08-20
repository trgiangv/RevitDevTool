using DevTools.TestAdapter;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace DevTools.TestAdapter.Tests;

public sealed class HostTestSessionTests
{
    private static readonly object DiscoveryProviderLock = new();

    [Fact]
    public void ScaleForRun_multiplies_per_test_timeout()
    {
        var options = new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe");
        var scaled = HostTestFramework.ScaleForRun(options, testCount: 3);
        Assert.Equal(180, scaled.PerTestTimeoutSeconds);
        Assert.Equal(60, options.PerTestTimeoutSeconds);
    }

    [Fact]
    public void SelectCases_throws_when_nunit_mtp_is_not_registered()
    {
        lock (DiscoveryProviderLock)
        {
            var previous = HostTestDiscovery.Provider;
            HostTestDiscovery.Provider = null;
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(
                    () => HostTestFramework.SelectCases(
                        typeof(HostTestSessionTests).Assembly.Location,
                        new TestingSelection([])));
                Assert.Contains(TestingPlatformBuilderHook.NUnitMTPAssemblyFileName, ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                HostTestDiscovery.Provider = previous;
            }
        }
    }

    [Fact]
    public void ToHostSelection_forwards_opaque_ids_from_already_matched_cases()
    {
        var matched = new[]
        {
            new TestingDiscoveredTest(
                "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)",
                "TestCase_Addition(1,1,2)",
                "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)"),
        };
        var selection = HostTestFramework.ToRunnerFilter(
            new TestNodeUidListFilter([new TestNodeUid("TestCase_Addition")]));

        var host = HostTestFramework.ToHostSelection(selection, matched);

        Assert.False(string.IsNullOrWhiteSpace(host.ProviderPayload));
        Assert.Contains(matched[0].TestId, host.ProviderPayload!, StringComparison.Ordinal);
        Assert.DoesNotContain("re=\"1\"", host.ProviderPayload!, StringComparison.Ordinal);
        Assert.Empty(host.TestIds);
        Assert.Null(host.Names);
    }

    [Fact]
    public void ToHostSelection_keeps_cli_name_filters()
    {
        var selection = new TestingSelection([], Names: ["Span_is_one_on_each_axis"]);
        var stub = new TestingDiscoveredTest(
            "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests.Span_is_one_on_each_axis",
            "Span_is_one_on_each_axis",
            "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests.Span_is_one_on_each_axis",
            "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests",
            "Span_is_one_on_each_axis");

        var host = HostTestFramework.ToHostSelection(selection, [stub]);

        Assert.Empty(host.TestIds);
        Assert.Equal("Span_is_one_on_each_axis", Assert.Single(host.Names!));
    }

    [Fact]
    public void ToHostSelection_uid_list_is_addtest_full_name()
    {
        var stubId = "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests.Span_is_one_on_each_axis";
        var matched = new[]
        {
            new TestingDiscoveredTest(
                stubId,
                "Span_is_one_on_each_axis",
                stubId,
                "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests",
                "Span_is_one_on_each_axis"),
        };
        var selection = HostTestFramework.ToRunnerFilter(
            new TestNodeUidListFilter([new TestNodeUid(stubId)]));

        var host = HostTestFramework.ToHostSelection(selection, matched);

        Assert.Contains($"<test>{stubId}</test>", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Contains("<method>Span_is_one_on_each_axis</method>", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Empty(host.TestIds);
        Assert.Null(host.Names);
    }

    [Fact]
    public void ToHostSelection_uid_with_no_select_hits_still_pushes_collapsed_xml()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var host = HostTestFramework.ToHostSelection(new TestingSelection([stubId]), []);

        Assert.Contains($"<test>{stubId}</test>", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Contains("<method>Stub_leaf</method>", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Empty(host.TestIds);
        Assert.Null(host.Names);
    }

    [Fact]
    public void ResultsForUnreportedIds_covers_requested_uid_when_host_returns_nothing()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var request = new TestingSelection([stubId]);
        var discovered = new[]
        {
            new TestingDiscoveredTest(stubId, "Stub_leaf", stubId),
        };

        var missing = Assert.Single(
            HostTestFramework.ResultsForUnreportedIds(request, discovered, []));

        Assert.Equal(stubId, missing.TestId);
        Assert.Equal("Stub_leaf", missing.DisplayName);
        Assert.Equal("Failed", missing.Outcome);
        Assert.Equal(HostTestFramework.UnreportedFullNameMessage, missing.Message);
    }

    [Fact]
    public void ResultsForUnreportedIds_skips_ids_the_host_already_reported()
    {
        var id = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var host = new[]
        {
            new TestingCaseResult(id, "PlainTest_Passes", "Passed", 1, null, null, null, null, [], []),
        };

        Assert.Empty(HostTestFramework.ResultsForUnreportedIds(
            new TestingSelection([id]),
            [new TestingDiscoveredTest(id, "PlainTest_Passes", id)],
            host));
    }

    [Fact]
    public void ResultsForUnreportedIds_uses_uid_when_select_missed()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var missing = Assert.Single(
            HostTestFramework.ResultsForUnreportedIds(new TestingSelection([stubId]), [], []));

        Assert.Equal(stubId, missing.TestId);
        Assert.Equal(stubId, missing.DisplayName);
        Assert.Equal("Failed", missing.Outcome);
    }

    [Fact]
    public void FoldHostResults_maps_expanded_fixture_leaves_onto_the_stub_uid()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved";
        var request = new TestingSelection([stubId]);
        var discovered = new[]
        {
            new TestingDiscoveredTest(stubId, "FixtureSource_ValueIsPreserved", stubId),
        };
        var host = new[]
        {
            new TestingCaseResult(
                stubId.Replace("ParameterizedFixture.", "ParameterizedFixture(3).", StringComparison.Ordinal),
                "FixtureSource_ValueIsPreserved",
                "Passed",
                4,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: stubId.Replace("ParameterizedFixture.", "ParameterizedFixture(3).", StringComparison.Ordinal)),
            new TestingCaseResult(
                stubId.Replace("ParameterizedFixture.", "ParameterizedFixture(\"fixture-source\").", StringComparison.Ordinal),
                "FixtureSource_ValueIsPreserved",
                "Passed",
                5,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: stubId.Replace("ParameterizedFixture.", "ParameterizedFixture(\"fixture-source\").", StringComparison.Ordinal)),
        };

        var folded = Assert.Single(HostTestFramework.FoldHostResults(request, discovered, host));

        Assert.Equal(stubId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
        Assert.Equal(9, folded.DurationMilliseconds);
    }

    [Fact]
    public void FoldHostResults_maps_setname_leaves_via_parent_suite_id()
    {
        var stubId = "DevTools.NUnit.SampleTests.BoundingBoxCaseSourceTests.Box_source_has_positive_span";
        var host = new[]
        {
            new TestingCaseResult(
                "DevTools.NUnit.SampleTests.BoundingBoxCaseSourceTests.Wide_box",
                "Wide_box",
                "Passed",
                3,
                null,
                null,
                null,
                null,
                [],
                [],
                ParentTestId: stubId,
                FullName: "DevTools.NUnit.SampleTests.BoundingBoxCaseSourceTests.Wide_box"),
        };

        var folded = Assert.Single(
            HostTestFramework.FoldHostResults(new TestingSelection([stubId]), [], host));

        Assert.Equal(stubId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
    }

    [Fact]
    public void FoldHostResults_publishes_testname_leaves_when_request_is_method_fqn()
    {
        const string methodId = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        const string namedOne = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        const string namedTwo = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_two";
        var discovered = new[]
        {
            new TestingDiscoveredTest(namedOne, "Named_one", namedOne, MethodName: "Original_named"),
            new TestingDiscoveredTest(namedTwo, "Named_two", namedTwo, MethodName: "Original_named"),
        };
        var host = new[]
        {
            new TestingCaseResult(
                namedOne,
                "Named_one",
                "Passed",
                2,
                null,
                null,
                null,
                null,
                [],
                [],
                ParentTestId: methodId,
                FullName: namedOne),
            new TestingCaseResult(
                namedTwo,
                "Named_two",
                "Passed",
                3,
                null,
                null,
                null,
                null,
                [],
                [],
                ParentTestId: methodId,
                FullName: namedTwo),
        };

        var folded = HostTestFramework.FoldHostResults(new TestingSelection([methodId]), discovered, host);

        Assert.Equal(3, folded.Count);
        Assert.Equal(methodId, folded[0].TestId);
        Assert.Equal("Passed", folded[0].Outcome);
        Assert.Equal(5, folded[0].DurationMilliseconds);
        Assert.Equal([namedOne, namedTwo], folded.Skip(1).Select(result => result.TestId).ToArray());
    }

    [Fact]
    public void FoldHostResults_does_not_starve_leaf_when_group_and_leaf_are_requested()
    {
        const string methodId = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        const string namedOne = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var host = new[]
        {
            new TestingCaseResult(
                namedOne,
                "Named_one",
                "Passed",
                2,
                null,
                null,
                null,
                null,
                [],
                [],
                ParentTestId: methodId,
                FullName: namedOne),
        };

        var folded = HostTestFramework.FoldHostResults(
            new TestingSelection([methodId, namedOne]),
            [new TestingDiscoveredTest(namedOne, "Named_one", namedOne)],
            host);

        Assert.Contains(folded, result => result.TestId == methodId);
        Assert.Contains(folded, result => result.TestId == namedOne);
    }

    [Fact]
    public void FoldHostResults_maps_nunit_fullname_onto_ide_testname_uid()
    {
        const string ideId =
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")";
        const string nunitName = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var discovered = new TestingDiscoveredTest(
            ideId,
            "Named_one",
            nunitName,
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
            "Original_named");
        var host = new[]
        {
            new TestingCaseResult(
                nunitName,
                "Named_one",
                "Passed",
                2,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: nunitName),
        };

        var folded = Assert.Single(
            HostTestFramework.FoldHostResults(new TestingSelection([ideId]), [discovered], host));

        Assert.Equal(ideId, folded.TestId);
        Assert.Equal("Named_one", folded.DisplayName);
        Assert.Equal("Passed", folded.Outcome);
    }

    [Fact]
    public void FoldHostResults_keeps_name_filter_leaves_unmapped()
    {
        var host = new[]
        {
            new TestingCaseResult(
                "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(3).FixtureSource_ValueIsPreserved",
                "FixtureSource_ValueIsPreserved",
                "Passed",
                1,
                null,
                null,
                null,
                null,
                [],
                []),
        };

        var folded = HostTestFramework.FoldHostResults(
            new TestingSelection([], Names: ["FixtureSource_ValueIsPreserved"]),
            [],
            host);

        Assert.Equal(host[0].TestId, Assert.Single(folded).TestId);
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
                lock (DiscoveryProviderLock)
                {
                    var previous = HostTestDiscovery.Provider;
                    HostTestDiscovery.Provider = new StubHostTestDiscoverer();
                    try
                    {
                        var nodes = HostTestFramework.DiscoverNodes(
                            typeof(HostTestSessionTests).Assembly.Location,
                            new TestingSelection([]));
                        Assert.NotNull(nodes);
                        Assert.NotEmpty(nodes);
                    }
                    finally
                    {
                        HostTestDiscovery.Provider = previous;
                    }
                }
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
        });

        var options = HostOptionsLoader.Load(configuration);

        Assert.Equal("Civil3D", options.HostName);
        Assert.Equal("2026", options.HostVersion);
        Assert.True(options.ForceLaunch);
        Assert.Equal(90, options.PerTestTimeoutSeconds);
        Assert.Equal(240, options.LaunchTimeoutSeconds);
        Assert.Equal(HostOptionsLoader.DefaultFrameworkId, options.FrameworkId);
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
    public void ToDiscoveredNode_uses_test_id_as_uid()
    {
        var node = HostTestFramework.ToDiscoveredNode(
            new TestingDiscoveredTest("HostSmokeTests.Arithmetic", "Arithmetic", "HostSmokeTests.Arithmetic"));

        Assert.Equal("HostSmokeTests.Arithmetic", node.Uid.Value);
        Assert.Equal("Arithmetic", node.DisplayName);
        Assert.NotNull(node.Properties.SingleOrDefault<DiscoveredTestNodeStateProperty>());
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("HostSmokeTests", identity.TypeName);
        Assert.Equal("Arithmetic", identity.MethodName);
    }

    [Fact]
    public void ToDiscoveredNode_groups_fixture_source_by_parameterized_type()
    {
        var fullName =
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved";
        var node = HostTestFramework.ToDiscoveredNode(
            new TestingDiscoveredTest(
                fullName,
                "Fixture_argument_is_preserved",
                fullName,
                "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\")",
                "Fixture_argument_is_preserved",
                new TestingSourceLocation(@"C:\src\FixtureShapeTests.cs", 55)));

        Assert.Equal(fullName, node.Uid.Value);
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("DevTools.NUnit.SampleTests", identity.Namespace);
        Assert.Equal("NamedFixtureSourceTests(\"alpha.rvt\")", identity.TypeName);
        Assert.Equal("Fixture_argument_is_preserved", identity.MethodName);
        var location = node.Properties.Single<TestFileLocationProperty>();
        Assert.Equal(@"C:\src\FixtureShapeTests.cs", location.FilePath);
        Assert.Equal(55, location.LineSpan.Start.Line);
    }

    [Fact]
    public void TrySplitIdentity_does_not_treat_fixture_arguments_as_the_method()
    {
        Assert.True(HostTestFramework.TrySplitIdentity(
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved",
            "Fixture_argument_is_preserved",
            className: null,
            methodName: null,
            out var ns,
            out var typeName,
            out var method));

        Assert.Equal("DevTools.NUnit.SampleTests", ns);
        Assert.Equal("NamedFixtureSourceTests(\"alpha.rvt\")", typeName);
        Assert.Equal("Fixture_argument_is_preserved", method);
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
    public void ToResultNode_uses_test_id_as_uid()
    {
        var node = HostTestFramework.ToResultNode(
            new TestingCaseResult(
                "HostSmokeTests.Arithmetic",
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
    public void ToResultNode_keeps_csharp_method_identifier_for_testname_leaf()
    {
        const string uid = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var discovered = new TestingDiscoveredTest(
            uid,
            "Named_one",
            uid,
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
            "Original_named");

        var discoveredId = HostTestFramework.ToDiscoveredNode(discovered)
            .Properties.Single<TestMethodIdentifierProperty>();
        var resultId = HostTestFramework.ToResultNode(
                new TestingCaseResult(
                    uid,
                    "Named_one",
                    "Passed",
                    1,
                    null,
                    null,
                    null,
                    null,
                    [],
                    [],
                    FullName: uid),
                assemblyPath: null,
                [discovered])
            .Properties.Single<TestMethodIdentifierProperty>();

        Assert.Equal("Original_named", discoveredId.MethodName);
        Assert.Equal(discoveredId.Namespace, resultId.Namespace);
        Assert.Equal(discoveredId.TypeName, resultId.TypeName);
        Assert.Equal(discoveredId.MethodName, resultId.MethodName);
    }

    [Fact]
    public void ToResultNode_without_discovery_treats_testname_as_the_method()
    {
        var identity = HostTestFramework.ToResultNode(
                new TestingCaseResult(
                    "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one",
                    "Named_one",
                    "Passed",
                    1,
                    null,
                    null,
                    null,
                    null,
                    [],
                    [],
                    FullName: "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one"))
            .Properties.Single<TestMethodIdentifierProperty>();

        Assert.Equal("Named_one", identity.MethodName);
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

internal sealed class StubHostTestDiscoverer : IHostTestDiscoverer
{
    public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath) =>
        [new TestingDiscoveredTest("Stub.Test", "Test", "Stub.Test")];

    public IReadOnlyList<TestingDiscoveredTest> Select(string assemblyPath, TestingSelection selection) =>
        Discover(assemblyPath);
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
