using DevTools.Testing.Abstractions;

namespace DevTools.TUnit.MTP;

public static class TUnitMTP
{
    public static void Register() => HostTestDiscovery.Provider = new TUnitHostTestDiscoverer();
}
