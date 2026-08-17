using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Results;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;

namespace DevTools.NUnit.Host;

internal static class NUnitTestingMapper
{
    public static TestingCaseResult ToTesting(NUnitCaseResult result)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        return new TestingCaseResult(
            result.Id,
            result.Name,
            result.Outcome,
            result.DurationMs,
            result.Message,
            result.StackTrace,
            result.Output,
            ToTesting(result.Source),
            ToTesting(result.Traits),
            ToTesting(result.Attachments),
            result.ParentTestId,
            result.FullName,
            result.SkipReason);
    }

    public static TestingEvent ToTesting(NUnitProgressEvent progressEvent)
    {
        if (progressEvent is null)
            throw new ArgumentNullException(nameof(progressEvent));

        return new TestingEvent(
            progressEvent.RunId,
            TestingEventKinds.Case,
            ToTesting(progressEvent.Case),
            Message: null,
            Attachment: null,
            TestingCancellationState.None);
    }

    public static TestingRunResponse ToTesting(
        NUnitRunResponse response,
        string frameworkId)
    {
        if (response is null)
            throw new ArgumentNullException(nameof(response));

        var results = response.Cases.Select(ToTesting).ToList();
        return new TestingRunResponse(
            response.RunId,
            frameworkId,
            response.GenerationId,
            results,
            ResolveCancellationState(response),
            response.RuntimeDiagnostic?.Code,
            response.RuntimeDiagnostic?.Message);
    }

    public static void Publish(NUnitProgressEvent progressEvent, ITestingEventSink eventSink)
    {
        if (eventSink is null)
            throw new ArgumentNullException(nameof(eventSink));

        eventSink.Publish(ToTesting(progressEvent));
    }

    private static TestingCancellationState ResolveCancellationState(NUnitRunResponse response)
    {
        if (response.Summary.Cancelled > 0)
            return TestingCancellationState.Completed;

        foreach (var testCase in response.Cases)
        {
            if (string.Equals(testCase.Outcome, NUnitOutcomes.Cancelled, StringComparison.Ordinal))
                return TestingCancellationState.Completed;
        }

        return TestingCancellationState.None;
    }

    private static TestingSourceLocation? ToTesting(NUnitSourceLocation? source) =>
        source is null ? null : new TestingSourceLocation(source.File, source.Line);

    private static IReadOnlyList<TestingTrait> ToTesting(IReadOnlyList<NUnitTrait>? traits)
    {
        if (traits is null || traits.Count == 0)
            return Array.Empty<TestingTrait>();

        return traits
            .Select(trait => new TestingTrait(trait.Name, trait.Value))
            .ToList();
    }

    private static IReadOnlyList<TestingAttachment> ToTesting(IReadOnlyList<NUnitAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
            return Array.Empty<TestingAttachment>();

        return attachments
            .Select(attachment => new TestingAttachment(
                attachment.Path,
                attachment.Name,
                attachment.ContentType,
                attachment.Base64))
            .ToList();
    }
}
