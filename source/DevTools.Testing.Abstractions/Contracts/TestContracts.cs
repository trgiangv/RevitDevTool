namespace DevTools.Testing.Abstractions.Contracts;

public sealed record TestHostOptions(
    string HostName,
    string HostVersion,
    bool ForceLaunch,
    int PerTestTimeoutSeconds,
    int LaunchTimeoutSeconds,
    int? DebugParentPid = null,
    int RequestTimeoutSeconds = 0)
{
    public int EffectiveRequestTimeoutSeconds =>
        RequestTimeoutSeconds > 0 ? RequestTimeoutSeconds : PerTestTimeoutSeconds;
}

public sealed record TestAssemblyReference(string Path);

public enum TestSelectionKind
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
public sealed record TestSelection
{
    public TestSelectionKind Kind { get; init; }
    public IReadOnlyList<string> TestIds { get; init; }
    public string? FilterFormat { get; init; }
    public string? FilterData { get; init; }
    public IReadOnlyList<string> Names { get; init; }

    public TestSelection(
        TestSelectionKind kind,
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
        Validate(kind);
    }

    public bool IsConstrained => Kind != TestSelectionKind.All;

    public static TestSelection All { get; } = new(TestSelectionKind.All);

    public static TestSelection FromTestIds(IReadOnlyList<string>? ids) =>
        new(TestSelectionKind.TestIds, testIds: ids);

    public static TestSelection FromFrameworkFilter(string format, string data) =>
        new(TestSelectionKind.FrameworkFilter, filterFormat: format, filterData: data);

    public static TestSelection FromNames(IReadOnlyList<string> names) =>
        new(TestSelectionKind.Names, names: names);

    private void Validate(TestSelectionKind kind)
    {
        switch (kind)
        {
            case TestSelectionKind.All:
                ValidateAll();
                break;
            case TestSelectionKind.TestIds:
                ValidateTestIds();
                break;
            case TestSelectionKind.FrameworkFilter:
                ValidateFrameworkFilter();
                break;
            case TestSelectionKind.Names:
                ValidateNames();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown selection kind.");
        }
    }

    private void ValidateAll()
    {
        if (HasIds || HasNames || HasFrameworkFilter)
            throw new ArgumentException("All selection cannot carry ids, names, or a framework filter.");
    }

    private void ValidateTestIds()
    {
        if (HasNames || HasFrameworkFilter)
            throw new ArgumentException("TestIds selection cannot carry names or a framework filter.");
    }

    private void ValidateFrameworkFilter()
    {
        if (string.IsNullOrWhiteSpace(FilterFormat) || string.IsNullOrWhiteSpace(FilterData))
            throw new ArgumentException("FrameworkFilter requires Format and Data.");
        if (HasIds || HasNames)
            throw new ArgumentException("FrameworkFilter cannot carry TestIds or Names.");
    }

    private void ValidateNames()
    {
        if (Names.Count == 0)
            throw new ArgumentException("Names selection requires at least one name.");
        if (HasIds || HasFrameworkFilter)
            throw new ArgumentException("Names selection cannot carry TestIds or a framework filter.");
    }

    private bool HasIds => TestIds.Count > 0;
    private bool HasNames => Names.Count > 0;
    private bool HasFrameworkFilter =>
        !string.IsNullOrWhiteSpace(FilterFormat) || !string.IsNullOrWhiteSpace(FilterData);
}

public sealed record TestDiscoveredTest(
    string TestId,
    string DisplayName,
    string? FullName = null,
    string? ClassName = null,
    string? MethodName = null,
    TestSourceLocation? Source = null,
    string? Namespace = null,
    string? TypeName = null,
    [property: UsedImplicitly] int MethodArity = 0,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<string>? ParameterTypeFullNames = null,
    string? ReturnTypeFullName = null);

public sealed record TestRunRequest
{
    public TestRunRequest(
        int ProtocolVersion,
        Guid RunId,
        TestFrameworkId FrameworkId,
        TestAssemblyReference Assembly,
        TestSelection Selection)
    {
        this.ProtocolVersion = ProtocolVersion;
        this.RunId = RunId;
        this.FrameworkId = FrameworkId;
        this.Assembly = Assembly;
        this.Selection = Selection;
    }

    public int ProtocolVersion { get; init; }
    public Guid RunId { get; init; }
    public TestFrameworkId FrameworkId
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(FrameworkId),
                    value,
                    "Unknown test framework.");
            }

            field = value;
        }
    }
    public TestAssemblyReference Assembly { get; init; }
    public TestSelection Selection { get; init; }
}

/// <summary>
/// Adapter → TestRunner execute envelope. Nested <see cref="Run"/> is the
/// same <see cref="TestRunRequest"/> the host receives on <c>testing/run</c>.
/// </summary>
public sealed record TestRunExecute(
    int ProtocolVersion,
    TestHostOptions Host,
    TestRunRequest Run);

public sealed record TestAttachment(
    string Path,
    string? Description,
    string? ContentType = null);

public sealed record TestSourceLocation(string File, int Line);
public sealed record TestTrait(string Name, string Value);

public sealed record TestCaseResult(
    string TestId,
    string DisplayName,
    string Outcome,
    double DurationMilliseconds,
    string? Message,
    string? StackTrace,
    string? Output,
    TestSourceLocation? Source,
    IReadOnlyList<TestTrait> Traits,
    IReadOnlyList<TestAttachment> Attachments,
    string? ParentTestId = null,
    string? FullName = null,
    string? SkipReason = null);

public enum TestCancellationState
{
    None,
    Requested,
    Acknowledged,
    Completed,
    Poisoned,
}

public static class TestEventKinds
{
    public const string Case = "case";
    public const string Output = "output";
    public const string Attachment = "attachment";
    public const string Diagnostic = "diagnostic";
    public const string Cancellation = "cancellation";
}

public static class TestOutcomes
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
    public const string Inconclusive = "Inconclusive";
    public const string Error = "Error";
    public const string Cancelled = "Cancelled";
}

public sealed record TestEvent(
    Guid RunId,
    string Kind,
    TestCaseResult? Case,
    string? Message,
    TestAttachment? Attachment,
    TestCancellationState CancellationState);

public sealed record TestRunResponse(
    Guid RunId,
    TestFrameworkId FrameworkId,
    string? GenerationId,
    IReadOnlyList<TestCaseResult> Results,
    TestCancellationState CancellationState,
    string? DiagnosticCode,
    string? DiagnosticMessage);
