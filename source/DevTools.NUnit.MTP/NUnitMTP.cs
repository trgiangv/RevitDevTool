using DevTools.Testing.Abstractions;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Plug-in entry loaded via testconfig.json <c>mtpEntry</c>; assigns
/// <see cref="HostTestDiscovery"/>.
/// </summary>
public static class NUnitMTP
{
    public static void Register()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        HostTestDiscovery.Provider = discoverer;
        HostTestDiscovery.RunMapper = discoverer;
    }
}
