namespace Build.Trains;

public static class NuGetPublish
{
    public const string KeyVariable = "NUGET_API_KEY";
    private const string Source = "https://api.nuget.org/v3/index.json";
    private const string PackageId = "RevitDevTool.TestAdapter";

    public static string PackagePath(string root, Train train, string version) =>
        Path.Combine(root, "output", train.Name, $"{PackageId}.{version}.nupkg");

    public static string Run(string root, Train train)
    {
        var version = VersionFile.Read(root, train.VersionFile);
        if (version.StartsWith("0.0.0", StringComparison.Ordinal) || ReleaseVersion.Parse(version, train.Grammar) is not { } parsed)
        {
            throw new InvalidOperationException($"Version '{version}' is not a release version; set it in {train.VersionFile}.");
        }

        var package = PackagePath(root, train, version);
        var notes = Path.Combine(Path.GetDirectoryName(package)!, "notes.md");
        if (!File.Exists(package) || !File.Exists(notes))
        {
            throw new InvalidOperationException($"Expected '{package}' and '{notes}' from the build job; download its artifact into {Path.GetDirectoryName(package)}.");
        }

        var key = Environment.GetEnvironmentVariable(KeyVariable);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"Set {KeyVariable} from the NuGet trusted publishing login before publish.");
        }

        Tool.Require(root, "dotnet", "nuget", "push", package, "--source", Source, "--api-key", key, "--skip-duplicate");

        var tag = train.Tag(version);
        if (Tool.Run(root, "gh", "release", "view", tag) == 0)
        {
            return $"Release {tag} already exists; nothing to create.";
        }

        var arguments = new List<string>
        {
            "release", "create", tag, package,
            "--target", Git.Run(root, "rev-parse", "HEAD").Trim(),
            "--title", $"{PackageId} {version}",
            "--notes-file", notes,
            "--latest=false"
        };
        if (parsed.IsPrerelease) arguments.Add("--prerelease");

        Tool.Require(root, "gh", [.. arguments]);
        return $"Published {PackageId} {version} as {tag}.";
    }
}
