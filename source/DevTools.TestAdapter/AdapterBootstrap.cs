namespace DevTools.TestAdapter;

internal static class AdapterBootstrap
{
    internal static void Initialize()
    {
        try
        {
            RuntimeAssemblyResolver.EnsureRegistered();
            if (!AdapterTestConfig.TryReadPluginConfig(out var config, out var error))
            {
                HostMTPRegistration.LastError = error;
                return;
            }

            HostMTPRegistration.Register(
                config!.MTPAssembly,
                config.MTPEntry,
                AppContext.BaseDirectory,
                RuntimeAssemblyResolver.LoadUnlocked);
        }
        catch (Exception ex)
        {
            HostMTPRegistration.LastError = ex.ToString();
        }
    }
}
