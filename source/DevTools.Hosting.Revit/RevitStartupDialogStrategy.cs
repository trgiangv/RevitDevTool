namespace DevTools.Hosting.Revit;

public sealed class RevitStartupDialogStrategy : IHostStartupDialogStrategy
{
    public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;

    public StartupDialogResolverOptions CreateOptions() => new()
    {
        DialogTitleKeywords = ["unsigned add-in"],
        PreferredButtonKeywords = ["always load"],
        BlockedButtonKeywords = ["do not load", "load once"],
        WindowClassName = "#32770",
        ButtonClassName = "button",
    };
}
