using DevTools.Testing.Abstractions;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Plug-in entry loaded by <see cref="DevTools.Testing.Abstractions.MTP.HostMTPRegistration"/>.
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
