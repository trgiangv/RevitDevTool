namespace DevTools.NUnit.Provider;

public sealed record NUnitTrait(string Name, string Value);
public sealed record NUnitSourceLocation(string File, int Line);
public sealed record NUnitDiscoveredTest(
    string Id,
    string Name,
    string FullName,
    string? ParentTestId = null,
    IReadOnlyList<NUnitTrait>? Traits = null,
    NUnitSourceLocation? Source = null,
    string? SkipReason = null);
