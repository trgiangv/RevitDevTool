using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using RevitDevTool.Utils;

namespace RevitDevTool.CodeExecute.Providers.FSharp;

internal static class NugetManager
{
    private static readonly string CacheRoot = Path.Combine(SettingsUtils.GetApplicationDataPath(), "nuget");
    private const string NugetServiceIndexUrl = "https://api.nuget.org/v3/index.json";

    private static readonly ConcurrentDictionary<string, RuntimeReferenceSet> SessionCache = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };
    private static string? _packageBaseUrl;

    public static async Task<string[]> ResolvePackageDllsAsync(string packageId, string? version, CancellationToken ct)
    {
        var resolvedVersion = version ?? await FetchLatestVersionAsync(packageId, ct).ConfigureAwait(false);
        var cacheKey = $"{packageId.ToLowerInvariant()}/{resolvedVersion.ToLowerInvariant()}";

        if (SessionCache.TryGetValue(cacheKey, out var cached))
            return cached.GetReferencesForCurrentRuntime(packageId, resolvedVersion);

        var packageDir = Path.Combine(CacheRoot, packageId.ToLowerInvariant(), resolvedVersion.ToLowerInvariant());
        var runtimeSet = MarkerFile.TryRead(Path.Combine(packageDir, MarkerFile.FileName))
            ?? await DownloadAndExtractAsync(packageId, resolvedVersion, packageDir, ct).ConfigureAwait(false);

        MarkerFile.Write(Path.Combine(packageDir, MarkerFile.FileName), runtimeSet);
        SessionCache[cacheKey] = runtimeSet;
        return runtimeSet.GetReferencesForCurrentRuntime(packageId, resolvedVersion);
    }

    private static async Task<string> FetchLatestVersionAsync(string packageId, CancellationToken ct)
    {
        var baseUrl = await GetPackageBaseUrlAsync(ct).ConfigureAwait(false);
        var url = $"{baseUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";

        using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(payload);

        var versions = doc.RootElement.GetProperty("versions")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        if (versions.Length == 0)
            throw new InvalidOperationException($"NuGet returned no versions for package '{packageId}'.");

        return versions.LastOrDefault(v => !v.Contains('-')) ?? versions.Last();
    }

    private static async Task<string> GetPackageBaseUrlAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_packageBaseUrl))
            return _packageBaseUrl!;

        using var response = await Http.GetAsync(NugetServiceIndexUrl, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(payload);

        foreach (var resource in doc.RootElement.GetProperty("resources").EnumerateArray())
        {
            var type = resource.TryGetProperty("@type", out var el) ? el.GetString() ?? "" : "";
            if (!type.StartsWith("PackageBaseAddress", StringComparison.OrdinalIgnoreCase))
                continue;

            _packageBaseUrl = resource.GetProperty("@id").GetString()
                ?? throw new InvalidOperationException("NuGet PackageBaseAddress '@id' is empty.");
            return _packageBaseUrl;
        }

        throw new InvalidOperationException("NuGet service index missing PackageBaseAddress resource.");
    }

    private static async Task<RuntimeReferenceSet> DownloadAndExtractAsync(
        string packageId, string version, string destDir, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);

        var baseUrl = await GetPackageBaseUrlAsync(ct).ConfigureAwait(false);
        var id = packageId.ToLowerInvariant();
        var ver = version.ToLowerInvariant();
        var url = $"{baseUrl.TrimEnd('/')}/{id}/{ver}/{id}.{ver}.nupkg";

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return ExtractAllTfms(archive, destDir, packageId, version);
    }

    private static RuntimeReferenceSet ExtractAllTfms(
        ZipArchive archive, string destDir, string packageId, string version)
    {
        var byTfm = GroupEntriesByTfm(archive);
        var availableTfms = byTfm.Keys.OrderBy(k => k).ToArray();

        if (byTfm.Count == 0)
        {
            Trace.TraceWarning($"[NuGetResolver] {packageId} {version}: no lib/ dlls found.");
            return RuntimeReferenceSet.Empty;
        }

        var tfmReferences = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in byTfm)
            tfmReferences[kvp.Key] = ExtractTfm(kvp.Value, destDir, kvp.Key);

        Trace.TraceInformation(
            $"[NuGetResolver] {packageId} {version}: extracted TFMs [{string.Join(", ", availableTfms)}]");

        return new RuntimeReferenceSet(tfmReferences, availableTfms);
    }

    private static Dictionary<string, List<ZipArchiveEntry>> GroupEntriesByTfm(ZipArchive archive)
    {
        return archive.Entries
            .Where(e =>
                e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
                e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.StartsWith("_", StringComparison.Ordinal))
            .GroupBy(e =>
            {
                var parts = e.FullName.Split('/');
                return parts.Length >= 3 ? parts[1].ToLowerInvariant() : "";
            })
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ExtractTfm(IEnumerable<ZipArchiveEntry> entries, string destDir, string tfm)
    {
        var tfmDir = Path.Combine(destDir, tfm);
        Directory.CreateDirectory(tfmDir);

        var extracted = new List<string>();
        foreach (var entry in entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(tfmDir, entry.Name);
            if (!File.Exists(path))
                entry.ExtractToFile(path, overwrite: false);
            extracted.Add(path);
        }

        return extracted.ToArray();
    }

}

internal sealed class RuntimeReferenceSet(Dictionary<string, string[]> tfmReferences, string[] availableTfms)
{
    public static RuntimeReferenceSet Empty { get; } = new(new Dictionary<string, string[]>(), []);

    public Dictionary<string, string[]> TfmReferences { get; } = tfmReferences;
    public string[] AvailableTfms { get; } = availableTfms;

    public string[] GetReferencesForCurrentRuntime(string packageId, string version)
    {
        var priority = BuildPriorityForCurrentRuntime();

        foreach (var tfm in priority)
        {
            if (TfmReferences.TryGetValue(tfm, out var refs) && refs.Length > 0)
                return refs;
        }

        var hasAnyRefs = TfmReferences.Any(kv => kv.Value.Length > 0);
        if (!hasAnyRefs)
            throw new InvalidOperationException(
                $"Package '{packageId} {version}' contains no runtime assemblies. " +
                $"Available TFMs: [{string.Join(", ", AvailableTfms)}].");

        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        throw new InvalidOperationException(
            $"Package '{packageId} {version}' has no TFM compatible with current runtime ({runtime}). " +
            $"Available TFMs: [{string.Join(", ", AvailableTfms)}]. " +
            $"Checked: [{string.Join(", ", priority)}].");
    }

    private static string[] BuildPriorityForCurrentRuntime()
    {
        var ver = Environment.Version;

        if (ver.Major == 4)
        {
            return ["net48", "net472", "net471", "net47", "net462", "net461",
                     "net46", "net45", "net40", "net35", "netstandard2.0"];
        }

        var list = new List<string>();
        for (var major = ver.Major; major >= 5; major--)
            list.Add($"net{major}.0");

        list.AddRange(["netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.1",
                        "netstandard2.1", "netstandard2.0"]);

        return list.ToArray();
    }
}

internal readonly record struct PackageRequest(string PackageId, string? Version);

internal static class MarkerFile
{
    public const string FileName = ".resolved";

    private const string Header = "#fsharp-nuget-resolver-v3";
    private const char Separator = '|';
    private const string AvailableTag = "tfms";

    public static void Write(string path, RuntimeReferenceSet refs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = new List<string>
        {
            Header,
            $"{AvailableTag}{Separator}{string.Join(";", refs.AvailableTfms)}"
        };
        lines.AddRange(from kvp in refs.TfmReferences from dllPath in kvp.Value select $"{kvp.Key}{Separator}{dllPath}");

        File.WriteAllLines(path, lines);
    }

    public static RuntimeReferenceSet? TryRead(string path)
    {
        if (!File.Exists(path)) return null;

        var lines = File.ReadAllLines(path);
        if (lines.Length == 0 || lines[0] != Header) return null;

        var tfmBuckets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var availableTfms = Array.Empty<string>();

        foreach (var line in lines.AsSpan(1))
        {
            var (tag, value) = SplitLine(line);
            if (tag == null) continue;

            if (tag.Equals(AvailableTag, StringComparison.OrdinalIgnoreCase))
                availableTfms = ParseAvailableTfms(value);
            else
                AppendTfmReference(tfmBuckets, tag, value);
        }

        var tfmReferences = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in tfmBuckets)
            tfmReferences[kvp.Key] = kvp.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return new RuntimeReferenceSet(tfmReferences, availableTfms);
    }

    private static (string? Tag, string Value) SplitLine(string line)
    {
        var sep = line.IndexOf(Separator);
        if (sep <= 0 || sep >= line.Length - 1) return (null, "");

        var tag = line[..sep];
        var value = line[(sep + 1)..];
        return string.IsNullOrWhiteSpace(value) ? (null, "") : (tag, value);
    }

    private static string[] ParseAvailableTfms(string value)
    {
        return value.Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim()).Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AppendTfmReference(Dictionary<string, List<string>> buckets, string tfm, string dllPath)
    {
        if (!File.Exists(dllPath)) return;

        if (!buckets.TryGetValue(tfm, out var bucket))
        {
            bucket = [];
            buckets[tfm] = bucket;
        }

        bucket.Add(dllPath);
    }
}
