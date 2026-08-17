using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitTestingMapperTests
{
    [Fact]
    public void ToTesting_uses_authoritative_id_not_display_name()
    {
        var nunit = new NUnitCaseResult(
            "assembly/FullSemanticsFixture#0/PlainTest_Passes#0",
            "PlainTest_Passes",
            NUnitOutcomes.Passed,
            12.5,
            "message",
            "stack",
            "output",
            ParentTestId: "assembly/FullSemanticsFixture#0",
            Traits:
            [
                new NUnitTrait("Category", "AcceptanceCategory"),
                new NUnitTrait("AcceptanceKey", "AcceptanceValue"),
            ],
            Source: new NUnitSourceLocation("FullSemanticsFixture.cs", 42),
            SkipReason: null,
            Attachments: [new NUnitAttachment("log", "text/plain", @"C:\tmp\log.txt", null)],
            FullName: "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes");

        var mapped = NUnitTestingMapper.ToTesting(nunit);

        Assert.Equal(nunit.Id, mapped.TestId);
        Assert.NotEqual(nunit.Name, mapped.TestId);
        Assert.Equal(nunit.Name, mapped.DisplayName);
        Assert.Equal(nunit.Outcome, mapped.Outcome);
        Assert.Equal(nunit.DurationMs, mapped.DurationMilliseconds);
        Assert.Equal(nunit.Message, mapped.Message);
        Assert.Equal(nunit.StackTrace, mapped.StackTrace);
        Assert.Equal(nunit.Output, mapped.Output);
        Assert.Equal(nunit.Source!.File, mapped.Source!.File);
        Assert.Equal(nunit.Source.Line, mapped.Source.Line);
        Assert.Equal(2, mapped.Traits.Count);
        Assert.Contains(mapped.Traits, trait => trait is { Name: "Category", Value: "AcceptanceCategory" });
        var attachment = Assert.Single(mapped.Attachments);
        Assert.Equal(@"C:\tmp\log.txt", attachment.Path);
        Assert.Equal("log", attachment.Description);
    }

    [Fact]
    public void ToTesting_uses_skip_reason_when_message_is_missing()
    {
        var nunit = new NUnitCaseResult(
            "id",
            "Ignored_IsSkipped",
            NUnitOutcomes.Skipped,
            0,
            Message: null,
            StackTrace: null,
            Output: null,
            SkipReason: "acceptance-ignore");

        var mapped = NUnitTestingMapper.ToTesting(nunit);
        Assert.Equal("acceptance-ignore", mapped.Message);
    }

    [Fact]
    public void ToTesting_run_response_maps_generation_and_cancellation()
    {
        var runId = Guid.NewGuid();
        var cancelled = new NUnitCaseResult("id", "name", NUnitOutcomes.Cancelled, 1, null, null, null);
        var response = new NUnitRunResponse(
            runId,
            new NUnitRunSummary(0, 0, 0, 0, 0, 1),
            [cancelled],
            "generation-1",
            new NUnitRuntimeDiagnostic("code", "detail"));

        var mapped = NUnitTestingMapper.ToTesting(response, TestingFrameworkIds.NUnit);

        Assert.Equal(runId, mapped.RunId);
        Assert.Equal(TestingFrameworkIds.NUnit, mapped.FrameworkId);
        Assert.Equal("generation-1", mapped.GenerationId);
        Assert.Equal(TestingCancellationState.Completed, mapped.CancellationState);
        Assert.Equal("code", mapped.DiagnosticCode);
        Assert.Equal("detail", mapped.DiagnosticMessage);
        Assert.Equal("id", Assert.Single(mapped.Results).TestId);
    }
}
