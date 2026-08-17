namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Host-API exact names and prefixes for the process this add-in is loaded into.
/// Add-ins pass this to <see cref="HostSharedAssemblies.Use"/> at startup.
/// </summary>
public sealed record HostSharedAssemblyNames(
    IReadOnlyCollection<string> ExactNames,
    IReadOnlyCollection<string> Prefixes);
