namespace DevTools.Testing.Abstractions.Contracts;

public sealed record TestingHostOptions(
    string HostName,
    string HostVersion,
    bool ForceLaunch,
    int PerTestTimeoutSeconds,
    int LaunchTimeoutSeconds,
    string? RunnerPath,
    int? DebugParentPid = null,
    string? FrameworkId = null,
    int RequestTimeoutSeconds = 0)
{
    public int EffectiveRequestTimeoutSeconds =>
        RequestTimeoutSeconds > 0 ? RequestTimeoutSeconds : PerTestTimeoutSeconds;
}

public sealed record TestingAssemblyReference(string Path);

public enum TestingSelectionKind
{
    All,
    TestIds,
    FrameworkFilter,
    Names,
}

/// <summary>
/// Closed selection. <see cref="All"/> is unconstrained.
/// <see cref="TestIds"/> with an empty list means "run nothing", not "run all".
/// <see cref="Names"/> is testhost/CLI convenience; mappers convert it before
/// <c>testing/run</c> when they need a stable host filter.
/// </summary>
public sealed record TestingSelection
{
    public TestingSelectionKind Kind { get; init; }
    public IReadOnlyList<string> TestIds { get; init; }
    public string? FilterFormat { get; init; }
    public string? FilterData { get; init; }
    public IReadOnlyList<string> Names { get; init; }

    public TestingSelection(
        TestingSelectionKind kind,
        IReadOnlyList<string>? testIds = null,
        string? filterFormat = null,
        string? filterData = null,
        IReadOnlyList<string>? names = null)
    {
        Kind = kind;
        TestIds = testIds ?? [];
        FilterFormat = filterFormat;
        FilterData = filterData;
        Names = names ?? [];
        Validate();
    }

    public bool IsConstrained => Kind != TestingSelectionKind.All;

    public static TestingSelection All { get; } = new(TestingSelectionKind.All);

    public static TestingSelection FromTestIds(IReadOnlyList<string>? ids) =>
        new(TestingSelectionKind.TestIds, testIds: ids);

    public static TestingSelection FromFrameworkFilter(string format, string data) =>
        new(TestingSelectionKind.FrameworkFilter, filterFormat: format, filterData: data);

    public static TestingSelection FromNames(IReadOnlyList<string> names) =>
        new(TestingSelectionKind.Names, names: names);

    /// <summary>Opaque XML filter payload consumed by the in-host engine.</summary>
    public const string XmlFilterFormat = "filter-xml";

    private void Validate()
    {
        switch (Kind)
        {
            case TestingSelectionKind.All:
                if (TestIds.Count > 0 || Names.Count > 0
                    || !string.IsNullOrWhiteSpace(FilterFormat) || !string.IsNullOrWhiteSpace(FilterData))
                {
                    throw new ArgumentException("All selection cannot carry ids, names, or a framework filter.");
                }

                break;
            case TestingSelectionKind.TestIds:
                if (Names.Count > 0 || !string.IsNullOrWhiteSpace(FilterFormat) || !string.IsNullOrWhiteSpace(FilterData))
                    throw new ArgumentException("TestIds selection cannot carry names or a framework filter.");
                break;
            case TestingSelectionKind.FrameworkFilter:
                if (string.IsNullOrWhiteSpace(FilterFormat) || string.IsNullOrWhiteSpace(FilterData))
                    throw new ArgumentException("FrameworkFilter requires Format and Data.");
                if (TestIds.Count > 0 || Names.Count > 0)
                    throw new ArgumentException("FrameworkFilter cannot carry TestIds or Names.");
                break;
            case TestingSelectionKind.Names:
                if (Names.Count == 0)
                    throw new ArgumentException("Names selection requires at least one name.");
                if (TestIds.Count > 0 || !string.IsNullOrWhiteSpace(FilterFormat) || !string.IsNullOrWhiteSpace(FilterData))
                    throw new ArgumentException("Names selection cannot carry TestIds or a framework filter.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown selection kind.");
        }
    }
}

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
    IReadOnlyList<string>? Categories = null);

public sealed record TestingRunRequest
{
    public TestingRunRequest(
        int ProtocolVersion,
        Guid RunId,
        string FrameworkId,
        TestingAssemblyReference Assembly,
        TestingSelection Selection)
    {
        this.ProtocolVersion = ProtocolVersion;
        this.RunId = RunId;
        this.FrameworkId = FrameworkId;
        this.Assembly = Assembly;
        this.Selection = Selection;
    }

    public int ProtocolVersion { get; init; }
    public Guid RunId { get; init; }
    public string FrameworkId
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(FrameworkId));
            field = value;
        }
    } = string.Empty;
    public TestingAssemblyReference Assembly { get; init; }
    public TestingSelection Selection { get; init; }
}

/// <summary>
/// Adapter → TestRunner machine invocation. Nested <see cref="Run"/> is the
/// same <see cref="TestingRunRequest"/> the host receives on <c>testing/run</c>.
/// </summary>
public sealed record TestingRunInvocation(
    int ProtocolVersion,
    TestingHostOptions Host,
    TestingRunRequest Run);

public sealed record TestingAttachment(
    string Path,
    string? Description,
    string? ContentType = null);

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
    IReadOnlyList<TestingAttachment> Attachments,
    string? ParentTestId = null,
    string? FullName = null,
    string? SkipReason = null);

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
