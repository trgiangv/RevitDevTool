using DevTools.Testing.Abstractions;

namespace DevTools.TUnit.MTP;

/// <summary>
/// Plug-in entry loaded by <see cref="DevTools.Testing.Abstractions.MTP.HostMTPRegistration"/>.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TUnitMTP
{
    public static void Register()
    {
        var bridge = new TUnitHostTestDiscoverer();
        HostTestDiscovery.Provider = bridge;
        HostTestDiscovery.RunMapper = bridge;
    }
}
