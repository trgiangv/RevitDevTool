namespace DevTools.Hosting.Acad;

public sealed class AcadStartupDialogSpec : IHostStartupDialogSpec
{
    public bool Supports(HostApp hostApp) => hostApp.IsAcadFamily();

    public StartupDialogOptions CreateOptions() => new()
    {
        DialogTitleKeywords = ["unsigned executable file"],
        PreferredButtonKeywords = ["always load"],
        BlockedButtonKeywords = ["do not load", "load once"],
        WindowClassName = "#32770",
        ButtonClassName = "button",
    };
}
