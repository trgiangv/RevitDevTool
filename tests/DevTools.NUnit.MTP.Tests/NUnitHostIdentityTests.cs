using DevTools.NUnit.MTP;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

public sealed class NUnitHostIdentityTests
{
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

        var host = new NUnitHostTestDiscoverer().ToHostSelection(
            new TestingSelection(["TestCase_Addition"]),
            matched);

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

        var host = new NUnitHostTestDiscoverer().ToHostSelection(selection, [stub]);

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

        var host = new NUnitHostTestDiscoverer().ToHostSelection(new TestingSelection([stubId]), matched);

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
        var host = new NUnitHostTestDiscoverer().ToHostSelection(new TestingSelection([stubId]), []);

        Assert.Contains($"<test>{stubId}</test>", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Contains("<method>Stub_leaf</method>", host.ProviderPayload, StringComparison.Ordinal);
        Assert.Empty(host.TestIds);
        Assert.Null(host.Names);
    }

    [Fact]
    public void ResultsForUnreported_covers_requested_uid_when_host_returns_nothing()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var request = new TestingSelection([stubId]);
        var discovered = new[]
        {
            new TestingDiscoveredTest(stubId, "Stub_leaf", stubId),
        };

        var missing = Assert.Single(
            new NUnitHostTestDiscoverer().ResultsForUnreported(request, discovered, []));

        Assert.Equal(stubId, missing.TestId);
        Assert.Equal("Stub_leaf", missing.DisplayName);
        Assert.Equal("Failed", missing.Outcome);
        Assert.Equal(NUnitHostTestDiscoverer.UnreportedFullNameMessage, missing.Message);
    }

    [Fact]
    public void ResultsForUnreported_skips_ids_the_host_already_reported()
    {
        var id = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var host = new[]
        {
            new TestingCaseResult(id, "PlainTest_Passes", "Passed", 1, null, null, null, null, [], []),
        };

        Assert.Empty(new NUnitHostTestDiscoverer().ResultsForUnreported(
            new TestingSelection([id]),
            [new TestingDiscoveredTest(id, "PlainTest_Passes", id)],
            host));
    }

    [Fact]
    public void ResultsForUnreported_uses_uid_when_select_missed()
    {
        var stubId = "DevTools.NUnit.Runtime.Fixtures.CollapsedSourceStubFixture.Stub_leaf";
        var missing = Assert.Single(
            new NUnitHostTestDiscoverer().ResultsForUnreported(new TestingSelection([stubId]), [], []));

        Assert.Equal(stubId, missing.TestId);
        Assert.Equal(stubId, missing.DisplayName);
        Assert.Equal("Failed", missing.Outcome);
    }

    [Fact]
    public void FoldResults_maps_expanded_fixture_leaves_onto_the_stub_uid()
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

        var folded = Assert.Single(new NUnitHostTestDiscoverer().FoldResults(request, discovered, host));

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
            new NUnitHostTestDiscoverer().FoldResults(new TestingSelection([stubId]), [], host));

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

        var folded = new NUnitHostTestDiscoverer().FoldResults(new TestingSelection([methodId]), discovered, host);

        Assert.Equal(3, folded.Count);
        Assert.Equal(methodId, folded[0].TestId);
        Assert.Equal("Passed", folded[0].Outcome);
        Assert.Equal(5, folded[0].DurationMilliseconds);
        Assert.Equal([namedOne, namedTwo], folded.Skip(1).Select(result => result.TestId).ToArray());
    }

    [Fact]
    public void FoldResults_does_not_starve_leaf_when_group_and_leaf_are_requested()
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

        var folded = new NUnitHostTestDiscoverer().FoldResults(
            new TestingSelection([methodId, namedOne]),
            [new TestingDiscoveredTest(namedOne, "Named_one", namedOne)],
            host);

        Assert.Contains(folded, result => result.TestId == methodId);
        Assert.Contains(folded, result => result.TestId == namedOne);
    }

    [Fact]
    public void FoldResults_maps_nunit_fullname_onto_ide_testname_uid()
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
            new NUnitHostTestDiscoverer().FoldResults(new TestingSelection([ideId]), [discovered], host));

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
            new NUnitHostTestDiscoverer().FoldResults(new TestingSelection([]), [discovered], host));

        Assert.Equal(ideId, folded.TestId);
        Assert.Equal("Named_one", folded.DisplayName);
        Assert.Equal("Passed", folded.Outcome);
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
            new TestingCaseResult(
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
            new NUnitHostTestDiscoverer().FoldResults(
                new TestingSelection([]),
                [new TestingDiscoveredTest(stubId, "FixtureSource_ValueIsPreserved", stubId)],
                host));

        Assert.Equal(expanded, folded.TestId);
    }

    [Fact]
    public void FoldResults_keeps_name_filter_leaves_unmapped()
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

        var folded = new NUnitHostTestDiscoverer().FoldResults(
            new TestingSelection([], Names: ["FixtureSource_ValueIsPreserved"]),
            [],
            host);

        Assert.Equal(host[0].TestId, Assert.Single(folded).TestId);
    }
}
