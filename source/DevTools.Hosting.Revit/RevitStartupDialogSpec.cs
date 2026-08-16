namespace DevTools.Hosting.Revit;

public sealed class RevitStartupDialogSpec : IHostStartupDialogSpec
{
    public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;

    public StartupDialogOptions CreateOptions() => new()
    {
        DialogTitleKeywords = ["unsigned add-in"],
        PreferredButtonKeywords = ["always load"],
        BlockedButtonKeywords = ["do not load", "load once"],
        WindowClassName = "#32770",
        ButtonClassName = "button",
    };
}
