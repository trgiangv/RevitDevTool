namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Host-API simple names and prefixes for the process this add-in is loaded into.
/// Add-ins pass this to <see cref="HostSharedAssemblies.Use"/> at startup.
/// </summary>
public sealed record HostApiAssemblySet(
    IReadOnlyCollection<string> SimpleNames,
    IReadOnlyCollection<string> Prefixes);
