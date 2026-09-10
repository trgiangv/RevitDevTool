using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.TestAdapter;

/// <summary>
/// Adapter-only settings. <see cref="Host"/> is the CAD
/// <see cref="TestHostOptions"/>; framework id and runner path never leave
/// this process on run stdin.
/// </summary>
internal sealed record TestRunSettings(
    TestHostOptions Host,
    TestFrameworkId FrameworkId,
    string? RunnerPath);
