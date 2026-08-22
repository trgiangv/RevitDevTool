namespace DevTools.Testing.Host.Loading;

/// <summary>
/// Host-owned runtime closure merged into a generation plan. Framework hosts
/// resolve paths beside the add-in; policies validate and copy the files.
/// </summary>
public sealed record HostRuntimeSource(
    string AssemblyPath,
    string? SymbolPath,
    IReadOnlyList<string> DependencyPaths);
