namespace Build.Trains;

public static class Git
{
    public static string Run(string root, params string[] arguments) => Command.Run("git", root, arguments);

    public static bool Succeeds(string root, params string[] arguments) => Command.Succeeds("git", root, arguments);

    public static IReadOnlyList<string> Lines(string root, params string[] arguments) =>
        Run(root, arguments).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static IReadOnlyList<string> Tags(string root, Train train) => Lines(root, "tag", "--list", $"{train.TagPrefix}*");
}
