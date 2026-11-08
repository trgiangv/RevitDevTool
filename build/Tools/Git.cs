using Build.Products;

namespace Build.Tools;

public static class Git
{
    public static string Run(string root, params string[] arguments) => Shell.Run(root, "git", arguments);

    public static bool Succeeds(string root, params string[] arguments) => Shell.Succeeds(root, "git", arguments);

    public static IReadOnlyList<string> Lines(string root, params string[] arguments) =>
        Run(root, arguments).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static IReadOnlyList<string> Tags(string root, Product product) => Lines(root, "tag", "--list", $"{product.TagPrefix}*");

    public static string Head(string root) => Run(root, "rev-parse", "HEAD").Trim();
}
