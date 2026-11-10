using Build.Products;
using Build.Tools;

namespace Build.Releases;

/// <summary>
///     <c>publish &lt;product&gt;</c> after the build job: pushes what <c>output/&lt;product&gt;</c> holds and creates the GitHub Release.
///     <c>nettest</c> pushes the nupkg to nuget.org; <c>daemon</c> and <c>addin</c> sign their feed and upload payloads before feeds.
/// </summary>
public static class Publish
{
    public const string NuGetKeyVariable = "NUGET_API_KEY";
    private const string NuGetSource = "https://api.nuget.org/v3/index.json";
    private const string AdapterPackageId = "RevitDevTool.TestAdapter";

    public static string Run(string root, Product product)
    {
        if (product == Product.PyTest) throw new InvalidOperationException("The pytest product publishes to PyPI from release.yml; there is no publish target for it.");

        var version = ProductVersion.FromFile(root, product);
        var output = product.Output(root);
        var notes = Path.Combine(output, "notes.md");
        if (!File.Exists(notes)) throw new InvalidOperationException($"Expected '{notes}' from the build job; download its artifact into {output}.");

        return product == Product.NetTest ? NuGet(root, version, output, notes) : Assets(root, version, output, notes);
    }

    private static string NuGet(string root, ProductVersion version, string output, string notes)
    {
        var package = Path.Combine(output, $"{AdapterPackageId}.{version.Version}.nupkg");
        if (!File.Exists(package)) throw new InvalidOperationException($"Expected '{package}' from the build job.");

        var key = Environment.GetEnvironmentVariable(NuGetKeyVariable);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException($"Set {NuGetKeyVariable} from the NuGet trusted publishing login before publish.");

        Shell.Stream(root, "dotnet", "nuget", "push", package, "--source", NuGetSource, "--api-key", key, "--skip-duplicate");
        if (Gh.ReleaseExists(root, version.Tag)) return $"Release {version.Tag} already exists; nothing to create.";

        Gh.Run(root, [.. CreateArguments(root, version, notes), package]);
        return $"Published {version.Title} as {version.Tag}.";
    }

    /// <summary>
    ///     A draft first, payloads, then feeds and their signatures, then published; a failure deletes the draft and its tag.
    /// </summary>
    private static string Assets(string root, ProductVersion version, string output, string notes)
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

        return $"Published {version.Title} as {version.Tag} with {payloads.Count} payloads and {feeds.Count} feed files.";
    }

    private static List<string> CreateArguments(string root, ProductVersion version, string notes)
    {
        List<string> arguments =
        [
            "release", "create", version.Tag,
            "--target", Git.Head(root),
            "--title", version.Title,
            "--notes-file", notes,
            "--latest=false"
        ];
        if (version.IsPrerelease) arguments.Add("--prerelease");
        return arguments;
    }
}
