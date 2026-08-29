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
                HostMtpRegistration.LastError = error;
                return;
            }

            HostMtpRegistration.Register(
                config!.MtpAssembly,
                config.MtpEntry,
                AppContext.BaseDirectory,
                RuntimeAssemblyResolver.LoadUnlocked);
        }
        catch (Exception ex)
        {
            HostMtpRegistration.LastError = ex.ToString();
        }
    }
}
