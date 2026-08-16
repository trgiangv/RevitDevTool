namespace DevTools.Hosting;

public interface IHostStartupDialogStrategy
{
    bool Supports(HostApp hostApp);

    StartupDialogResolverOptions CreateOptions();
}
