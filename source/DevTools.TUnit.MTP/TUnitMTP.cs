using DevTools.Testing.Abstractions;

namespace DevTools.TUnit.MTP;

/// <summary>
/// Plug-in entry loaded via testconfig.json <c>mtpEntry</c>; assigns
/// <see cref="HostTestDiscovery"/>.
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
