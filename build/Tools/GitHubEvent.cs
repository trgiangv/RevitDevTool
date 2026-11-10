using System.Text.Json.Nodes;

namespace Build.Tools;

/// <summary>
///     The commit a CI run compares against: the pull request or merge queue base, else the commit before the push.
/// </summary>
public static class GitHubEvent
{
    public static string? BaseSha()
    {
        var path = Environment.GetEnvironmentVariable("GITHUB_EVENT_PATH");
        return string.IsNullOrEmpty(path) || !File.Exists(path) ? null : BaseSha(JsonNode.Parse(File.ReadAllText(path)));
    }

    public static string? BaseSha(JsonNode? payload) =>
        (string?)payload?["pull_request"]?["base"]?["sha"]
        ?? (string?)payload?["merge_group"]?["base_sha"]
        ?? (string?)payload?["before"];
}
