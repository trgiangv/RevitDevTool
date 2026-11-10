using System.Text.Json;
using System.Text.Json.Serialization;

namespace Build.Products;

public sealed record CiJob(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("run")] string Run,
    [property: JsonPropertyName("cache")] string Cache,
    [property: JsonPropertyName("pack")] string? Pack = null,
    [property: JsonPropertyName("host_version")] string? HostVersion = null);

/// <summary>
///     The <c>ci.yml</c> matrix jobs of each product, written to <see cref="JobsFile"/>. Each job keeps its own NuGet cache, named by
///     <see cref="CiJob.Cache"/>. The add-in has one test job and one compile job per <c>Release.Autodesk.*</c> host version.
/// </summary>
public static class CiJobs
{
    public const string JobsFile = ".github/ci-jobs.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyList<CiJob> For(Product product, IEnumerable<string> hostVersions) =>
        product == Product.Addin
            ?
            [
                new CiJob("addin tests", "test addin", "addin-tests"),
                ..hostVersions.Select(version => new CiJob($"Autodesk {version}", $"compile addin --host-version {version}", $"autodesk-{version}", HostVersion: version))
            ]
            : [new CiJob(product.Name, $"ci {product.Name}", product.Name, product.Name)];

    public static string Render(IEnumerable<string> hostVersions)
    {
        var versions = hostVersions.ToArray();
        var jobs = Product.All.ToDictionary(product => product.Name, product => For(product, versions));
        return JsonSerializer.Serialize(jobs, Options).ReplaceLineEndings("\n") + "\n";
    }

    public static bool IsCurrent(string root, IEnumerable<string> hostVersions)
    {
        var path = Path.Combine(root, JobsFile);
        return File.Exists(path) && File.ReadAllText(path).ReplaceLineEndings("\n") == Render(hostVersions);
    }

    public static void Write(string root, IEnumerable<string> hostVersions) =>
        File.WriteAllText(Path.Combine(root, JobsFile), Render(hostVersions));
}
