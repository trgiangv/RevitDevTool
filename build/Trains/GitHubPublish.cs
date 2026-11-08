namespace Build.Trains;

/// <summary>
///     <c>publish daemon|addin</c>: signs the feed of the built release, checks it with the shipped public key, and creates the
///     GitHub Release: draft, payloads, feeds last, then published. A failure deletes the draft and its tag.
/// </summary>
public static class GitHubPublish
{
    public static string Title(TrainVersion version) =>
        version.Train == Train.Daemon ? $"RevitDevTool Daemon {version.Version}" : $"RevitDevTool add-in {version.Version}";

    public static string Run(string root, Train train)
    {
        var text = VersionFile.Read(root, train.VersionFile);
        if (text.StartsWith("0.0.0", StringComparison.Ordinal) || ReleaseVersion.Parse(text, train.Grammar) is not { } parsed)
            throw new InvalidOperationException($"Version '{text}' is not a release version; set it in {train.VersionFile}.");

        var version = new TrainVersion(train, text, parsed.IsPrerelease, null);
        var output = Path.Combine(root, "output", train.Name);
        var notes = Path.Combine(output, "notes.md");
        if (!File.Exists(notes))
            throw new InvalidOperationException($"Expected '{notes}' from the build job; download its artifact into {output}.");

        var key = Signing.Key();
        Signing.SignFile(Path.Combine(output, ReleaseAssets.FeedName(version)), key);
        ReleaseAssets.Verify(version, output, signed: true);

        var tag = version.Tag;
        if (Command.Succeeds("gh", root, "release", "view", tag))
        {
            if (Command.Run("gh", root, "release", "view", tag, "--json", "isDraft", "--jq", ".isDraft").Trim() != "true")
                return $"Release {tag} is already published; nothing to do.";

            Command.Run("gh", root, "release", "delete", tag, "--yes");
        }

        var (payloads, feeds) = ReleaseAssets.Split(version, Directory.GetFiles(output).Select(Path.GetFileName).OfType<string>());
        List<string> create =
        [
            "release", "create", tag,
            "--draft",
            "--target", Git.Run(root, "rev-parse", "HEAD").Trim(),
            "--title", Title(version),
            "--notes-file", notes,
            "--latest=false"
        ];
        if (version.IsPrerelease) create.Add("--prerelease");

        Command.Run("gh", root, [.. create]);
        try
        {
            foreach (var asset in payloads.Concat(feeds))
                Command.Run("gh", root, "release", "upload", tag, Path.Combine(output, asset), "--clobber");

            Command.Run("gh", root, "release", "edit", tag, "--draft=false", $"--latest={version.IsLatest.ToString().ToLowerInvariant()}");
        }
        catch
        {
            Command.Succeeds("gh", root, "release", "delete", tag, "--yes", "--cleanup-tag");
            throw;
        }

        return $"Published {Title(version)} as {tag} with {payloads.Count} payloads and {feeds.Count} feed files.";
    }
}
