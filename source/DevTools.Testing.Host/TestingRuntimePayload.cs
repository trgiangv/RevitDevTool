namespace DevTools.Testing.Host;

public sealed record TestingRuntimePayload(
    string FrameworkId,
    string TestAssemblyPath,
    string RuntimeAssemblyPath,
    string FrameworkAssemblyPath,
    IReadOnlyList<string> AdditionalProbeRoots);
