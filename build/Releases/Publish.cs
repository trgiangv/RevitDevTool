using Build.Tools;
using Build.Trains;

namespace Build.Releases;

/// <summary>
///     <c>publish &lt;train&gt;</c> after the build job: pushes what <c>output/&lt;train&gt;</c> holds and creates the GitHub Release.
///     <c>nettest</c> pushes the nupkg to nuget.org; <c>daemon</c> and <c>addin</c> sign their feed and upload payloads before feeds.
/// </summary>
public static class Publish
{
    public const string NuGetKeyVariable = "NUGET_API_KEY";
    private const string NuGetSource = "https://api.nuget.org/v3/index.json";
    private const string AdapterPackageId = "RevitDevTool.TestAdapter";

    public static string Title(TrainVersion version) => version.Train.Name switch
    {
        "daemon" => $"RevitDevTool Daemon {version.Version}",
        "addin" => $"RevitDevTool add-in {version.Version}",
        _ => $"{AdapterPackageId} {version.Version}"
    };

    public static string Run(string root, Train train)
    {
        if (train == Train.PyTest) throw new InvalidOperationException("The pytest train publishes to PyPI from release.yml; there is no publish target for it.");

        var version = TrainVersion.FromFile(root, train);
        var output = train.Output(root);
        var notes = Path.Combine(output, "notes.md");
        if (!File.Exists(notes)) throw new InvalidOperationException($"Expected '{notes}' from the build job; download its artifact into {output}.");

        return train == Train.NetTest ? NuGet(root, version, output, notes) : Assets(root, version, output, notes);
    }

    private static string NuGet(string root, TrainVersion version, string output, string notes)
    {
        var package = Path.Combine(output, $"{AdapterPackageId}.{version.Version}.nupkg");
        if (!File.Exists(package)) throw new InvalidOperationException($"Expected '{package}' from the build job.");

        var key = Environment.GetEnvironmentVariable(NuGetKeyVariable);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException($"Set {NuGetKeyVariable} from the NuGet trusted publishing login before publish.");

        Shell.Stream(root, "dotnet", "nuget", "push", package, "--source", NuGetSource, "--api-key", key, "--skip-duplicate");
        if (Gh.ReleaseExists(root, version.Tag)) return $"Release {version.Tag} already exists; nothing to create.";

        Gh.Run(root, [.. CreateArguments(root, version, notes), package]);
        return $"Published {Title(version)} as {version.Tag}.";
    }

    /// <summary>
    ///     A draft first, payloads, then feeds and their signatures, then published; a failure deletes the draft and its tag.
    /// </summary>
    private static string Assets(string root, TrainVersion version, string output, string notes)
    {
        Signing.SignFile(Path.Combine(output, ReleaseAssets.FeedName(version)), Signing.Key());
        ReleaseAssets.Verify(version, output, signed: true);

        if (Gh.ReleaseExists(root, version.Tag))
        {
            if (!Gh.IsDraft(root, version.Tag)) return $"Release {version.Tag} is already published; nothing to do.";
            Gh.Delete(root, version.Tag);
        }

        var (payloads, feeds) = ReleaseAssets.Split(version, Directory.GetFiles(output).Select(Path.GetFileName).OfType<string>());
        Gh.Run(root, [.. CreateArguments(root, version, notes), "--draft"]);
        try
        {
            foreach (var asset in payloads.Concat(feeds)) Gh.Upload(root, version.Tag, Path.Combine(output, asset));
            Gh.Run(root, "release", "edit", version.Tag, "--draft=false", $"--latest={version.IsLatest.ToString().ToLowerInvariant()}");
        }
        catch
        {
            Gh.Delete(root, version.Tag, withTag: true);
            throw;
        }

        return $"Published {Title(version)} as {version.Tag} with {payloads.Count} payloads and {feeds.Count} feed files.";
    }

    private static List<string> CreateArguments(string root, TrainVersion version, string notes)
    {
        List<string> arguments =
        [
            "release", "create", version.Tag,
            "--target", Git.Head(root),
            "--title", Title(version),
            "--notes-file", notes,
            "--latest=false"
        ];
        if (version.IsPrerelease) arguments.Add("--prerelease");
        return arguments;
    }
}
