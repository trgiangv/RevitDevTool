namespace DevTools.Hosting.Acad;

public sealed class AcadStartupDialogStrategy : IHostStartupDialogStrategy
{
    public bool Supports(HostApp hostApp) => hostApp.IsAcadFamily();

    public StartupDialogResolverOptions CreateOptions() => new()
    {
        DialogTitleKeywords = ["unsigned executable file"],
        PreferredButtonKeywords = ["always load"],
        BlockedButtonKeywords = ["do not load", "load once"],
        WindowClassName = "#32770",
        ButtonClassName = "button",
    };
}
