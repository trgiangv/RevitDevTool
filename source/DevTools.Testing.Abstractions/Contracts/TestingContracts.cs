namespace DevTools.Testing.Abstractions.Contracts;

public static class TestingFrameworkIds
{
    public const string NUnit = "nunit";
}

public sealed record TestingHostOptions(
    string Host,
    string HostVersion,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    string? RunnerPath,
    int? DebugParentPid = null);

public sealed record TestingAssemblyReference(
    string Path,
    string? TargetFramework,
    string? ContentHash);

public sealed record TestingSelection(
    IReadOnlyList<string> TestIds,
    string? ProviderPayload = null);

public sealed record TestingRunRequest(
    int ProtocolVersion,
    Guid RunId,
    string FrameworkId,
    TestingAssemblyReference Assembly,
    TestingSelection Selection,
    IReadOnlyDictionary<string, string> FrameworkOptions);

public sealed record TestingAttachment(string Path, string? Description);
public sealed record TestingSourceLocation(string File, int Line);
public sealed record TestingTrait(string Name, string Value);

public sealed record TestingCaseResult(
    string TestId,
    string DisplayName,
    string Outcome,
    double DurationMilliseconds,
    string? Message,
    string? StackTrace,
    string? Output,
    TestingSourceLocation? Source,
    IReadOnlyList<TestingTrait> Traits,
    IReadOnlyList<TestingAttachment> Attachments);

public enum TestingCancellationState
{
    None,
    Requested,
    Acknowledged,
    Completed,
    Poisoned,
}

public static class TestingEventKinds
{
    public const string Case = "case";
    public const string Output = "output";
    public const string Attachment = "attachment";
    public const string Diagnostic = "diagnostic";
    public const string Cancellation = "cancellation";
}

public sealed record TestingEvent(
    Guid RunId,
    string Kind,
    TestingCaseResult? Case,
    string? Message,
    TestingAttachment? Attachment,
    TestingCancellationState CancellationState);

public sealed record TestingRunResponse(
    Guid RunId,
    string FrameworkId,
    string? GenerationId,
    IReadOnlyList<TestingCaseResult> Results,
    TestingCancellationState CancellationState,
    string? DiagnosticCode,
    string? DiagnosticMessage);
