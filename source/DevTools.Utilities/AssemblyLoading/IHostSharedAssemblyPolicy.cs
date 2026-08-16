namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Host-API simple names and prefixes for the process this add-in is loaded into.
/// Implementations live in RevitDevTool / AcadDevTool — not in generic Hosting.
/// </summary>
public interface IHostSharedAssemblyPolicy
{
    IReadOnlyCollection<string> HostApiSimpleNames { get; }

    IReadOnlyCollection<string> HostApiPrefixes { get; }
}
