using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Core.Runtime;
using DevTools.NUnit.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[CollectionDefinition(nameof(AcceptanceFixtureCollection), DisableParallelization = true)]
public sealed class AcceptanceFixtureCollection;

public sealed class RecordingEventSink : INUnitRuntimeEventSink
{
    public List<NUnitRuntimeEvent> Events { get; } = [];

    public void Publish(NUnitRuntimeEvent runtimeEvent) => Events.Add(runtimeEvent);
}

[Collection(nameof(AcceptanceFixtureCollection))]
public sealed class NUnitRuntimeSessionTests
{
    [Fact]
    public void Discover_ReturnsExactExpandedFixtureMatrix()
    {
        using var session = FixtureTestHarness.CreateSession();
        var response = session.Discover(new NUnitDiscoverRequest(FixtureTestHarness.FixtureAssemblyPath, null));

        Assert.Equal(FixtureTestHarness.GenerationId, response.GenerationId);
        Assert.Equal(31, response.Cases.Count);

        var fullNames = response.Cases.Select(test => test.FullName).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.Equal(ExpectedDiscoveryFullNames, fullNames);

        var categoryCase = Assert.Single(response.Cases, test => test.FullName.EndsWith("CategoryAndProperty_AreAttached", StringComparison.Ordinal));
        Assert.Contains(categoryCase.Traits!, trait => trait is { Name: "Category", Value: "AcceptanceCategory" });
        Assert.Contains(categoryCase.Traits!, trait => trait is { Name: "AcceptanceKey", Value: "AcceptanceValue" });
        Assert.NotNull(categoryCase.Source);
        Assert.Contains("FullSemanticsFixture.cs", categoryCase.Source!.File, StringComparison.OrdinalIgnoreCase);

        var ignored = Assert.Single(response.Cases, test => test.FullName.EndsWith("Ignored_IsSkipped", StringComparison.Ordinal));
        Assert.Equal("acceptance-ignore", ignored.SkipReason);

        var explicitTest = Assert.Single(response.Cases, test => test.FullName.EndsWith("Explicit_RequiresExplicitSelection", StringComparison.Ordinal));
        Assert.Equal("acceptance-explicit", explicitTest.SkipReason);
    }

    [Fact]
    public void Discover_WithCategoryFilter_ReturnsSingleCase()
    {
        using var session = FixtureTestHarness.CreateSession();
        var response = session.Discover(new NUnitDiscoverRequest(
            FixtureTestHarness.FixtureAssemblyPath,
            "<filter><cat>AcceptanceCategory</cat></filter>"));

        var testCase = Assert.Single(response.Cases);
        Assert.EndsWith("CategoryAndProperty_AreAttached", testCase.FullName, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ExecutesRepresentativeSemanticsThroughNUnit()
    {
        FixtureTestHarness.ResetAcceptanceLog();
        using var session = FixtureTestHarness.CreateSession();
        var sink = new RecordingEventSink();
        var runId = Guid.NewGuid();

        var response = session.Run(
            new NUnitRunRequest(runId, FixtureTestHarness.FixtureAssemblyPath, null),
            sink,
            CancellationToken.None);

        Assert.Equal(FixtureTestHarness.GenerationId, response.GenerationId);
        Assert.Equal(31, response.Cases.Count);
        Assert.Equal(27, response.Summary.Passed);
        Assert.Equal(0, response.Summary.Failed);
        Assert.Equal(2, response.Summary.Skipped);
        Assert.Equal(1, response.Summary.Inconclusive);
        Assert.Equal(1, response.Summary.Errors);
        Assert.Equal(0, response.Summary.Cancelled);

        var plain = Assert.Single(response.Cases, test => test.Name == "PlainTest_Passes");
        Assert.Equal(NUnitOutcomes.Passed, plain.Outcome);

        var warning = Assert.Single(response.Cases, test => test.Name == "Warning_IsNonFatal");
        Assert.Equal(NUnitOutcomes.Passed, warning.Outcome);
        Assert.Contains("acceptance-warning", warning.Message, StringComparison.Ordinal);

        var inconclusive = Assert.Single(response.Cases, test => test.Name == "Inconclusive_TerminatesAsInconclusive");
        Assert.Equal(NUnitOutcomes.Inconclusive, inconclusive.Outcome);
        Assert.Equal("acceptance-inconclusive", inconclusive.Message);

        var error = Assert.Single(response.Cases, test => test.Name == "UnexpectedException_ThrowsOutsideAssertion");
        Assert.Equal(NUnitOutcomes.Error, error.Outcome);
        Assert.Contains("acceptance-unexpected-exception", error.Message, StringComparison.Ordinal);

        var ignored = Assert.Single(response.Cases, test => test.Name == "Ignored_IsSkipped");
        Assert.Equal(NUnitOutcomes.Skipped, ignored.Outcome);
        Assert.Equal("acceptance-ignore", ignored.SkipReason);

        var explicitTest = Assert.Single(response.Cases, test => test.Name == "Explicit_RequiresExplicitSelection");
        Assert.Equal(NUnitOutcomes.Skipped, explicitTest.Outcome);
        Assert.Equal("acceptance-explicit", explicitTest.SkipReason);

        var output = Assert.Single(response.Cases, test => test.Name == "Output_IsWrittenToTestContext");
        Assert.Contains("acceptance-output-marker", output.Output, StringComparison.Ordinal);
        Assert.Contains("acceptance-trace-marker", output.Output, StringComparison.Ordinal);
        Assert.Contains("acceptance-debug-marker", output.Output, StringComparison.Ordinal);

        var retryCase = Assert.Single(response.Cases, test => test.Name == "Retry_EventuallyPasses");
        Assert.Equal(NUnitOutcomes.Passed, retryCase.Outcome);
        Assert.Equal(3, FixtureTestHarness.ReadAcceptanceTokens().Count(token => token.StartsWith("FullSemanticsFixture.Retry_EventuallyPasses:attempt-", StringComparison.Ordinal)));

        var repeatCase = Assert.Single(response.Cases, test => test.Name == "Repeat_ExecutesMultipleTimes");
        Assert.Equal(NUnitOutcomes.Passed, repeatCase.Outcome);
        Assert.Equal(2, FixtureTestHarness.ReadAcceptanceTokens().Count(token => token.StartsWith("FullSemanticsFixture.Repeat_ExecutesMultipleTimes:invocation-", StringComparison.Ordinal)));

        var asyncCase = Assert.Single(response.Cases, test => test.Name == "AsyncTest_Completes");
        Assert.Equal(NUnitOutcomes.Passed, asyncCase.Outcome);

        var tokens = FixtureTestHarness.ReadAcceptanceTokens();
        Assert.Contains("AssemblySetUp.OneTimeSetUp", tokens);
        Assert.Contains("FullSemanticsFixture.OneTimeSetUp", tokens);
        Assert.Contains("OrderedSemanticsFixture.Ordered_First", tokens);
        Assert.Contains("AsyncLifecycleFixture.OneTimeSetUp", tokens);
        Assert.Contains("TestData.ExecutableCases", tokens);

        var finishedEvents = sink.Events
            .Where(runtimeEvent => runtimeEvent.Kind == NUnitRuntimeEventKinds.CaseFinished)
            .ToList();
        Assert.Equal(response.Cases.Count, finishedEvents.Count);
        Assert.Equal(
            response.Cases.Select(test => test.Id).Distinct(StringComparer.Ordinal).Count(),
            finishedEvents.Count);
        Assert.Contains(
            sink.Events,
            runtimeEvent => runtimeEvent.Kind == NUnitRuntimeEventKinds.CaseOutput
                && runtimeEvent.Message!.Contains("acceptance-output-marker", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_PublishesProgressBeforeFinalResponse()
    {
        FixtureTestHarness.ResetAcceptanceLog();
        using var session = FixtureTestHarness.CreateSession();
        var sink = new RecordingEventSink();
        NUnitRunResponse? finalResponse = null;

        finalResponse = session.Run(
            new NUnitRunRequest(
                Guid.NewGuid(),
                FixtureTestHarness.FixtureAssemblyPath,
                "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"),
            sink,
            CancellationToken.None);

        Assert.NotNull(finalResponse);
        var finished = Assert.Single(
            sink.Events,
            runtimeEvent => runtimeEvent.Kind == NUnitRuntimeEventKinds.CaseFinished);
        Assert.Equal(NUnitOutcomes.Passed, finished.Case!.Outcome);
    }

    private static readonly string[] ExpectedDiscoveryFullNames =
    [
        "DevTools.NUnit.Runtime.Fixtures.AsyncLifecycleFixture.AsyncLifecycle_TestCompletes",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.AsyncTest_Completes",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.CategoryAndProperty_AreAttached",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.ExecutableCases_alpha",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.ExecutableCases_beta",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Explicit_RequiresExplicitSelection",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.GenerationMarker_IsReported",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Ignored_IsSkipped",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Inconclusive_TerminatesAsInconclusive",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Lifecycle_SetUpPrecedesTearDown_ForThisTest",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.MultipleAssertions_AllReported",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Output_IsWrittenToTestContext",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Repeat_ExecutesMultipleTimes",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Retry_EventuallyPasses",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCaseSource_StaticProvider(2)",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCaseSource_StaticProvider(4)",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCaseSource_StaticProvider(6)",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(10,-4,6)",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(2,3,5)",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.UnexpectedException_ThrowsOutsideAssertion",
        "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.Warning_IsNonFatal",
        "DevTools.NUnit.Runtime.Fixtures.GenericFixture<Int32>.GenericFixture_UsesRequestedType",
        "DevTools.NUnit.Runtime.Fixtures.GenericFixture<String>.GenericFixture_UsesRequestedType",
        "DevTools.NUnit.Runtime.Fixtures.OrderedSemanticsFixture.Ordered_First",
        "DevTools.NUnit.Runtime.Fixtures.OrderedSemanticsFixture.Ordered_Second",
        "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(\"fixture-source\").FixtureSource_GenerationMarkerIsVisible",
        "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(\"fixture-source\").FixtureSource_ValueIsPreserved",
        "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(3).FixtureSource_GenerationMarkerIsVisible",
        "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(3).FixtureSource_ValueIsPreserved",
    ];
}
