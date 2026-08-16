namespace DevTools.Hosting;

/// <summary>
/// Per-host catalog of startup-dialog title/button keywords. Does not resolve or click dialogs —
/// that is <see cref="StartupDialogResolver"/>.
/// </summary>
public interface IHostStartupDialogSpec
{
    bool Supports(HostApp hostApp);

    StartupDialogOptions CreateOptions();
}
