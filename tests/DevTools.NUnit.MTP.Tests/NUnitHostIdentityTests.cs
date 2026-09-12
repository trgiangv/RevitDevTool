using DevTools.NUnit.MTP;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

public sealed class NUnitHostIdentityTests
{
    [Fact]
    public void ToRunSelection_forwards_opaque_ids_from_already_matched_cases()
    {
        var matched = new[]
        {
            new TestDiscoveredTest(
                "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)",
                "TestCase_Addition(1,1,2)",
                "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)"),
        };

        var host = new NUnitTestRunMapper().ToRunSelection(
            TestSelection.FromTestIds(["TestCase_Addition"]),
            matched);

        Assert.Equal(TestSelectionKind.FrameworkFilter, host.Kind);
        Assert.False(string.IsNullOrWhiteSpace(host.FilterData));
        Assert.Contains(matched[0].TestId, host.FilterData!, StringComparison.Ordinal);
        Assert.DoesNotContain("<method>TestCase_Addition</method>", host.FilterData!, StringComparison.Ordinal);
        Assert.Empty(host.TestIds);
        Assert.Empty(host.Names);
    }

    [Fact]
    public void ToRunSelection_keeps_cli_name_filters()
    {
        var selection = TestSelection.FromNames(["Span_is_one_on_each_axis"]);
        var stub = new TestDiscoveredTest(
            "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests.Span_is_one_on_each_axis",
            "Span_is_one_on_each_axis",
            "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests.Span_is_one_on_each_axis",
            "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests",
            "Span_is_one_on_each_axis");

        var host = new NUnitTestRunMapper().ToRunSelection(selection, [stub]);

        Assert.Empty(host.TestIds);
        Assert.Equal("Span_is_one_on_each_axis", Assert.Single(host.Names!));
    }

    [Fact]
    public void ToRunSelection_uid_list_is_addtest_full_name()
    {
        var stubId = "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests.Span_is_one_on_each_axis";
        var matched = new[]
        {
            new TestDiscoveredTest(
                stubId,
                "Span_is_one_on_each_axis",
                stubId,
                "DevTools.NUnit.SampleTests.BoundingBoxFixtureSourceTests",
                "Span_is_one_on_each_axis"),
        };

        var host = new NUnitTestRunMapper().ToRunSelection(TestSelection.FromTestIds([stubId]), matched);

        Assert.Contains($"<test>{stubId}</test>", host.FilterData, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", host.FilterData, StringComparison.Ordinal);
        Assert.Contains("<method>Span_is_one_on_each_axis</method>", host.FilterData, StringComparison.Ordinal);
        Assert.Empty(host.TestIds);
        Assert.Empty(host.Names);
    }

    [Fact]
    public void ToRunSelection_uid_with_no_select_hits_still_pushes_collapsed_xml()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var host = new NUnitTestRunMapper().ToRunSelection(TestSelection.FromTestIds([stubId]), []);

        Assert.Contains($"<test>{stubId}</test>", host.FilterData, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", host.FilterData, StringComparison.Ordinal);
        Assert.Contains("<method>Stub_leaf</method>", host.FilterData, StringComparison.Ordinal);
        Assert.Empty(host.TestIds);
        Assert.Empty(host.Names);
    }

    [Fact]
    public void ResultsForUnreported_covers_requested_uid_when_host_returns_nothing()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var request = TestSelection.FromTestIds([stubId]);
        var discovered = new[]
        {
            new TestDiscoveredTest(stubId, "Stub_leaf", stubId),
        };

        var missing = Assert.Single(
            new NUnitTestRunMapper().ResultsForUnreported(request, discovered, []));

        Assert.Equal(stubId, missing.TestId);
        Assert.Equal("Stub_leaf", missing.DisplayName);
        Assert.Equal("Failed", missing.Outcome);
        Assert.Equal(NUnitTestRunMapper.UnreportedFullNameMessage, missing.Message);
    }

    [Fact]
    public void ResultsForUnreported_skips_ids_the_host_already_reported()
    {
        var id = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var host = new[]
        {
            new TestCaseResult(id, "PlainTest_Passes", "Passed", 1, null, null, null, null, [], []),
        };

        Assert.Empty(new NUnitTestRunMapper().ResultsForUnreported(
            TestSelection.FromTestIds([id]),
            [new TestDiscoveredTest(id, "PlainTest_Passes", id)],
            host));
    }

    [Fact]
    public void ResultsForUnreported_uses_uid_when_select_missed()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var missing = Assert.Single(
            new NUnitTestRunMapper().ResultsForUnreported(TestSelection.FromTestIds([stubId]), [], []));

        Assert.Equal(stubId, missing.TestId);
        Assert.Equal(stubId, missing.DisplayName);
        Assert.Equal("Failed", missing.Outcome);
    }

    [Fact]
    public void FoldResults_maps_expanded_fixture_leaves_onto_the_stub_uid()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved";
        var request = TestSelection.FromTestIds([stubId]);
        var discovered = new[]
        {
            new TestDiscoveredTest(stubId, "FixtureSource_ValueIsPreserved", stubId),
        };
        var host = new[]
        {
            new TestCaseResult(
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
            new TestCaseResult(
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

        var folded = Assert.Single(new NUnitTestRunMapper().FoldResults(request, discovered, host));

        Assert.Equal(stubId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
        Assert.Equal(9, folded.DurationMilliseconds);
    }

    [Fact]
    public void FoldResults_maps_setname_leaves_via_parent_suite_id()
    {
        var stubId = "DevTools.NUnit.SampleTests.BoundingBoxCaseSourceTests.Box_source_has_positive_span";
        var host = new[]
        {
            new TestCaseResult(
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
            new NUnitTestRunMapper().FoldResults(TestSelection.FromTestIds([stubId]), [], host));

        Assert.Equal(stubId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
    }

    [Fact]
    public void FoldResults_publishes_testname_leaves_when_request_is_method_fqn()
    {
        const string methodId = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        const string namedOne = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        const string namedTwo = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_two";
        var discovered = new[]
        {
            new TestDiscoveredTest(namedOne, "Named_one", namedOne, MethodName: "Original_named"),
            new TestDiscoveredTest(namedTwo, "Named_two", namedTwo, MethodName: "Original_named"),
        };
        var host = new[]
        {
            new TestCaseResult(
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
            new TestCaseResult(
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

        var folded = new NUnitTestRunMapper().FoldResults(TestSelection.FromTestIds([methodId]), discovered, host);

        Assert.Equal([namedOne, namedTwo], folded.Select(result => result.TestId).ToArray());
        Assert.All(folded, result => Assert.Equal("Passed", result.Outcome));
    }

    [Fact]
    public void FoldResults_does_not_starve_leaf_when_group_and_leaf_are_requested()
    {
        const string methodId = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        const string namedOne = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var host = new[]
        {
            new TestCaseResult(
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

        var folded = new NUnitTestRunMapper().FoldResults(
            TestSelection.FromTestIds([methodId, namedOne]),
            [new TestDiscoveredTest(namedOne, "Named_one", namedOne)],
            host);

        Assert.DoesNotContain(folded, result => result.TestId == methodId);
        Assert.Contains(folded, result => result.TestId == namedOne);
    }

    [Fact]
    public void FoldResults_rider_group_uid_publishes_ide_testname_leaves()
    {
        const string methodId = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        const string ideId =
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")";
        const string nunitName = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var discovered = new TestDiscoveredTest(
            ideId,
            "Named_one",
            nunitName,
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
            "Original_named");
        var host = new[]
        {
            new TestCaseResult(
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
                ParentTestId: methodId,
                FullName: nunitName),
        };

        var mapper = new NUnitTestRunMapper();
        var folded = Assert.Single(
            mapper.FoldResults(TestSelection.FromTestIds([methodId]), [discovered], host));

        Assert.Equal(ideId, folded.TestId);
        Assert.Empty(mapper.ResultsForUnreported(
            TestSelection.FromTestIds([methodId]),
            [discovered],
            [folded]));
    }

    [Fact]
    public void FoldResults_maps_nunit_fullname_onto_ide_testname_uid()
    {
        const string ideId =
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")";
        const string nunitName = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var discovered = new TestDiscoveredTest(
            ideId,
            "Named_one",
            nunitName,
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
            "Original_named");
        var host = new[]
        {
            new TestCaseResult(
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
            new NUnitTestRunMapper().FoldResults(TestSelection.FromTestIds([ideId]), [discovered], host));

        Assert.Equal(ideId, folded.TestId);
        Assert.Equal("Named_one", folded.DisplayName);
        Assert.Equal("Passed", folded.Outcome);
    }

    [Fact]
    public void FoldResults_unfiltered_run_remaps_testname_leaf_onto_ide_uid()
    {
        const string ideId =
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")";
        const string nunitName = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var discovered = new TestDiscoveredTest(
            ideId,
            "Named_one",
            nunitName,
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
            "Original_named");
        var host = new[]
        {
            new TestCaseResult(
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
            new NUnitTestRunMapper().FoldResults(TestSelection.All, [discovered], host));

        Assert.Equal(ideId, folded.TestId);
        Assert.Equal("Named_one", folded.DisplayName);
        Assert.Equal("Passed", folded.Outcome);
    }

    [Fact]
    public void FoldResults_maps_host_double_args_without_suffix_onto_testhost_uid()
    {
        const string testhostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)";
        const string hostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var discovered = new TestDiscoveredTest(
            testhostId,
            "Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)",
            testhostId);
        var host = new[]
        {
            new TestCaseResult(
                hostId,
                "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
                "Passed",
                12,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: hostId),
        };

        var folded = Assert.Single(
            new NUnitTestRunMapper().FoldResults(
                TestSelection.FromTestIds([testhostId]),
                [discovered],
                host));

        Assert.Equal(testhostId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
        Assert.Empty(new NUnitTestRunMapper().ResultsForUnreported(
            TestSelection.FromTestIds([testhostId]),
            [discovered],
            [folded]));
    }

    [Fact]
    public void FoldResults_maps_parameterized_cases_onto_method_uid()
    {
        const string methodId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z";
        const string hostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var host = new[]
        {
            new TestCaseResult(
                hostId,
                "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
                "Passed",
                8,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: hostId),
        };

        var folded = Assert.Single(
            new NUnitTestRunMapper().FoldResults(
                TestSelection.FromTestIds([methodId]),
                [],
                host));

        Assert.Equal(methodId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
        Assert.Empty(new NUnitTestRunMapper().ResultsForUnreported(
            TestSelection.FromTestIds([methodId]),
            [],
            [folded]));
    }

    [Fact]
    public void FoldResults_rider_group_uid_publishes_discovered_leaves_not_ancestor()
    {
        const string methodId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z";
        const string testhostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)";
        const string hostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var discovered = new TestDiscoveredTest(
            testhostId,
            "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
            testhostId);
        var host = new[]
        {
            new TestCaseResult(
                hostId,
                "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
                "Passed",
                8,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: hostId),
        };

        var mapper = new NUnitTestRunMapper();
        var folded = Assert.Single(
            mapper.FoldResults(TestSelection.FromTestIds([methodId]), [discovered], host));

        Assert.Equal(testhostId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
        Assert.Empty(mapper.ResultsForUnreported(
            TestSelection.FromTestIds([methodId]),
            [discovered],
            [folded]));
    }

    [Fact]
    public void FoldResults_rider_empty_parens_group_uid_publishes_leaves()
    {
        const string methodId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z()";
        const string testhostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)";
        const string hostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var discovered = new TestDiscoveredTest(
            testhostId,
            "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
            testhostId);
        var host = new[]
        {
            new TestCaseResult(
                hostId,
                "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
                "Passed",
                8,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: hostId),
        };

        var mapper = new NUnitTestRunMapper();
        var folded = Assert.Single(
            mapper.FoldResults(TestSelection.FromTestIds([methodId]), [discovered], host));

        Assert.Equal(testhostId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
        Assert.Empty(mapper.ResultsForUnreported(
            TestSelection.FromTestIds([methodId]),
            [discovered],
            [folded]));
    }

    [Fact]
    public void FoldResults_visual_studio_display_name_uid_publishes_discovered_leaf()
    {
        const string displayName = "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        const string testhostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        const string hostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var discovered = new TestDiscoveredTest(
            testhostId,
            displayName,
            testhostId);
        var host = new[]
        {
            new TestCaseResult(
                hostId,
                displayName,
                "Passed",
                8,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: hostId),
        };

        var mapper = new NUnitTestRunMapper();
        var folded = Assert.Single(
            mapper.FoldResults(TestSelection.FromTestIds([displayName]), [discovered], host));

        Assert.Equal(testhostId, folded.TestId);
        Assert.Equal("Passed", folded.Outcome);
        Assert.Empty(mapper.ResultsForUnreported(
            TestSelection.FromTestIds([displayName]),
            [discovered],
            [folded]));
    }

    [Fact]
    public void ResultsForUnreported_fails_discovered_leaves_not_group_uid()
    {
        const string methodId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z";
        const string testhostId =
            "DevTools.NUnit.SampleTests.BoundingBoxXyzSampleTests.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)";
        var discovered = new TestDiscoveredTest(
            testhostId,
            "Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)",
            testhostId);

        var missing = Assert.Single(
            new NUnitTestRunMapper().ResultsForUnreported(
                TestSelection.FromTestIds([methodId]),
                [discovered],
                []));

        Assert.Equal(testhostId, missing.TestId);
        Assert.Equal("Failed", missing.Outcome);
    }

    [Fact]
    public void FoldResults_unfiltered_run_keeps_unmatched_stub_expansions()
    {
        const string stubId =
            "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved";
        var expanded =
            "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(3).FixtureSource_ValueIsPreserved";
        var host = new[]
        {
            new TestCaseResult(
                expanded,
                "FixtureSource_ValueIsPreserved",
                "Passed",
                4,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: expanded),
        };

        var folded = Assert.Single(
            new NUnitTestRunMapper().FoldResults(
                TestSelection.All,
                [new TestDiscoveredTest(stubId, "FixtureSource_ValueIsPreserved", stubId)],
                host));

        Assert.Equal(expanded, folded.TestId);
    }

    [Fact]
    public void FoldResults_keeps_name_filter_leaves_unmapped()
    {
        var host = new[]
        {
            new TestCaseResult(
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

        var folded = new NUnitTestRunMapper().FoldResults(
            TestSelection.FromNames(["FixtureSource_ValueIsPreserved"]),
            [],
            host);

        Assert.Equal(host[0].TestId, Assert.Single(folded).TestId);
    }
}
