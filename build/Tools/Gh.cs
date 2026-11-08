namespace Build.Tools;

/// <summary>
///     GitHub Releases through the <c>gh</c> CLI; <c>GH_TOKEN</c> comes from the workflow.
/// </summary>
public static class Gh
{
    public static string Run(string root, params string[] arguments) => Shell.Run(root, "gh", arguments);

    public static bool ReleaseExists(string root, string tag) => Shell.Succeeds(root, "gh", "release", "view", tag);

    public static bool IsDraft(string root, string tag) =>
        Run(root, "release", "view", tag, "--json", "isDraft", "--jq", ".isDraft").Trim() == "true";

    public static void Upload(string root, string tag, string path) => Run(root, "release", "upload", tag, path, "--clobber");

    public static void Download(string root, string tag, string folder, params string[] patterns) =>
        Run(root, ["release", "download", tag, .. patterns.SelectMany(pattern => new[] { "--pattern", pattern }), "--dir", folder, "--clobber"]);

    public static void Delete(string root, string tag, bool withTag = false) =>
        Shell.Succeeds(root, "gh", withTag ? ["release", "delete", tag, "--yes", "--cleanup-tag"] : ["release", "delete", tag, "--yes"]);
}
