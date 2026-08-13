namespace DevTools.NUnit.Core.Contracts;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitHelloRequest(int ProtocolVersion);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitHelloResponse(
    int ProtocolVersion,
    string Host,
    string HostVersion,
    int ProcessId,
    bool IsBusy);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitDiscoverRequest(string AssemblyPath, string? Filter);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitTrait(string Name, string Value);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitSourceLocation(string File, int Line);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitAttachment(
    string Name,
    string? ContentType,
    string? Path,
    string? Base64);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRuntimeDiagnostic(string Code, string Message);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitDiscoveredTest(
    string Id,
    string Name,
    string FullName,
    string? ParentTestId = null,
    IReadOnlyList<NUnitTrait>? Traits = null,
    NUnitSourceLocation? Source = null,
    string? SkipReason = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitDiscoverResponse(
    IReadOnlyList<NUnitDiscoveredTest> Cases,
    string? GenerationId = null,
    NUnitRuntimeDiagnostic? RuntimeDiagnostic = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRunRequest(Guid RunId, string AssemblyPath, string? Filter);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRunSummary(
    int Passed,
    int Failed,
    int Skipped,
    int Inconclusive,
    int Errors,
    int Cancelled);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitCaseResult(
    string Id,
    string Name,
    string Outcome,
    double DurationMs,
    string? Message,
    string? StackTrace,
    string? Output,
    string? ParentTestId = null,
    IReadOnlyList<NUnitTrait>? Traits = null,
    NUnitSourceLocation? Source = null,
    string? SkipReason = null,
    IReadOnlyList<NUnitAttachment>? Attachments = null,
    string? FullName = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRunResponse(
    Guid RunId,
    NUnitRunSummary Summary,
    IReadOnlyList<NUnitCaseResult> Cases,
    string? GenerationId = null,
    NUnitRuntimeDiagnostic? RuntimeDiagnostic = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitProgressEvent(Guid RunId, NUnitCaseResult Case);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitCancelRequest(Guid RunId);
