namespace RevitDevTool.Console.Services.Hosting;

public sealed class StartupDialogResolverOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    public int MaxNoButtonRetriesPerWindow { get; init; } = 3;

    public IReadOnlyList<string> DialogTitleKeywords { get; init; } =
    [
        "add-in",
        "addin",
        "questionable add-in",
        "unsigned add-in"
    ];

    public IReadOnlyList<string> PreferredButtonKeywords { get; init; } =
    [
        "always load",
        "load",
        "ok",
        "yes",
        "close",
        "continue"
    ];
}
