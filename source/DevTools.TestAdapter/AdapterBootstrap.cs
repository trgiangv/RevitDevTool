using DevTools.Testing.Abstractions.MTP;

namespace DevTools.TestAdapter;

internal static class AdapterBootstrap
{
    internal static void Initialize()
    {
        RuntimeAssemblyResolver.EnsureRegistered();
        HostMTPRegistration.RegisterForFramework(
            AdapterTestConfig.RequireFrameworkId(),
            AppContext.BaseDirectory,
            RuntimeAssemblyResolver.LoadUnlocked);
    }
}
