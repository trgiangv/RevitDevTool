namespace DevTools.Testing.Abstractions.Contracts;

public sealed record TestingHostOptions(
    string HostName,
    string HostVersion,
    bool ForceLaunch,
    int PerTestTimeoutSeconds,
    int LaunchTimeoutSeconds,
    string? RunnerPath,
    int? DebugParentPid = null,
    string? FrameworkId = null);

public sealed record TestingAssemblyReference(
    string Path,
    string? TargetFramework,
    string? ContentHash);

public sealed record TestingDiscoveryHints(
    IReadOnlyList<string>? ClassNames = null,
    IReadOnlyList<string>? MethodNames = null,
    IReadOnlyList<string>? Categories = null)
{
    public static TestingDiscoveryHints Empty { get; } = new();

    public bool IsEmpty =>
        IsBlank(ClassNames) && IsBlank(MethodNames) && IsBlank(Categories);

    static bool IsBlank(IReadOnlyList<string>? values) => values is null || values.Count == 0;
}

public sealed record TestingDiscoveryOptions(bool ForExecution = false)
{
    public static TestingDiscoveryOptions Testhost { get; } = new(ForExecution: false);

    public static TestingDiscoveryOptions HostRun { get; } = new(ForExecution: true);
}

public sealed record TestingSelection(
    IReadOnlyList<string> TestIds,
    string? ProviderPayload = null,
    IReadOnlyList<string>? Names = null,
    TestingDiscoveryHints? Hints = null);

public sealed record TestingDiscoveredTest(
    string TestId,
    string DisplayName,
    string? FullName = null,
    string? ClassName = null,
    string? MethodName = null,
    TestingSourceLocation? Source = null,
    string? Namespace = null,
    string? TypeName = null,
    [property: UsedImplicitly] int MethodArity = 0,
    bool HasDataSource = false,
    IReadOnlyList<string>? Categories = null);

public sealed record TestingRunRequest
{
    private string _frameworkId = string.Empty;

    public TestingRunRequest(
        int ProtocolVersion,
        Guid RunId,
        string FrameworkId,
        TestingAssemblyReference Assembly,
        TestingSelection Selection,
        IReadOnlyDictionary<string, string> FrameworkOptions)
    {
        this.ProtocolVersion = ProtocolVersion;
        this.RunId = RunId;
        this.FrameworkId = FrameworkId;
        this.Assembly = Assembly;
        this.Selection = Selection;
        this.FrameworkOptions = FrameworkOptions;
    }

    public int ProtocolVersion { get; init; }
    public Guid RunId { get; init; }
    public string FrameworkId
    {
        get => _frameworkId;
        init => _frameworkId = ValidateFrameworkId(value);
    }
    public TestingAssemblyReference Assembly { get; init; }
    public TestingSelection Selection { get; init; }
    public IReadOnlyDictionary<string, string> FrameworkOptions { get; init; }

    private static string ValidateFrameworkId(string frameworkId)
    {
        if (string.IsNullOrWhiteSpace(frameworkId))
            throw new ArgumentException("Framework ID is required.", nameof(FrameworkId));

        return frameworkId;
    }
}

public sealed record TestingAttachment(
    string? Path,
    string? Description,
    string? ContentType = null,
    string? Base64 = null);
public sealed record TestingSourceLocation(string File, int Line);
public sealed record TestingTrait(string Name, string Value);
public sealed record TestingProviderPayload(string Format, int Version, string Data);

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
    IReadOnlyList<TestingAttachment> Attachments,
    string? ParentTestId = null,
    string? FullName = null,
    string? SkipReason = null,
    TestingProviderPayload? ProviderPayload = null);

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

public static class TestingOutcomes
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
    public const string Inconclusive = "Inconclusive";
    public const string Error = "Error";
    public const string Cancelled = "Cancelled";
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
