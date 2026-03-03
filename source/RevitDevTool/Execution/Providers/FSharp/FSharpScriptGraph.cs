using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace RevitDevTool.Execution.Providers.FSharp;

// ReSharper disable once PartialTypeWithSinglePart
internal static partial class FSharpScriptGraph
{
    private const string NugetDirectivePattern = """^\s*#r\s+"nuget:\s*(?<id>[A-Za-z0-9._\-]+)(?:\s*,\s*(?<ver>[^"]+))?"\s*$""";
    private const string LoadDirectivePattern = """
                                                ^\s*#load\s+@?"(?<path>[^"]+)"
                                                """;
    private const string VersionPrefixPattern = @"^[><=~^*\s]+";
#if NETFRAMEWORK
    private static readonly Regex NugetDirectiveRx = new(NugetDirectivePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LoadDirectiveRx = new(LoadDirectivePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionPrefixRx = new(VersionPrefixPattern, RegexOptions.Compiled);
    private static Regex NugetDirectiveRegex() => NugetDirectiveRx;
    private static Regex LoadDirectiveRegex() => LoadDirectiveRx;
    private static Regex VersionPrefixRegex() => VersionPrefixRx;
#else
    [GeneratedRegex(NugetDirectivePattern, RegexOptions.IgnoreCase)]
    private static partial Regex NugetDirectiveRegex();

    [GeneratedRegex(LoadDirectivePattern, RegexOptions.IgnoreCase)]
    private static partial Regex LoadDirectiveRegex();

    [GeneratedRegex(VersionPrefixPattern)]
    private static partial Regex VersionPrefixRegex();
#endif

    private static readonly string TempCacheRoot =
        Path.Combine(Path.GetTempPath(), "RevitDevTool", "fsx_cache");

    public static async Task<LoadGraph> BuildLoadGraphAsync(string entryScript, CancellationToken ct)
    {
        var nodes = new Dictionary<string, ScriptNode>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageDirective>();
        var queue = new Queue<string>();
        queue.Enqueue(entryScript);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var filePath = queue.Dequeue();
            if (nodes.ContainsKey(filePath) || !File.Exists(filePath))
                continue;

            var node = await ParseScriptNodeAsync(filePath, ct).ConfigureAwait(false);
            nodes[filePath] = node;

            foreach (var target in node.LoadTargets.Values)
                queue.Enqueue(target);

            CollectPackageDirectives(filePath, node.Lines, packages);
        }

        return new LoadGraph(nodes, packages);
    }

    public static string ComputeGraphHash(LoadGraph graph)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var path in graph.Nodes.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
            hash.AppendData(File.ReadAllBytes(path));
        }

        return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
    }

    public static async Task<RewriteResult> RewriteGraphAsync(
        string entryScript,
        IReadOnlyDictionary<string, ScriptNode> nodes,
        CancellationToken ct)
    {
        var pathMap = BuildTempPathMap(entryScript, nodes.Keys);

        foreach (var node in nodes.Values)
        {
            ct.ThrowIfCancellationRequested();
            var rewritten = new List<string>(node.Lines.Length);
            for (var i = 0; i < node.Lines.Length; i++)
                rewritten.Add(TransformLine(node, i, pathMap));

            await WriteAllLinesAsync(pathMap[node.Path], rewritten, ct).ConfigureAwait(false);
        }

        return new RewriteResult(pathMap[entryScript],
            new TempFileCollection(pathMap.Values.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private static async Task<ScriptNode> ParseScriptNodeAsync(string filePath, CancellationToken ct)
    {
        var lines = await ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var loadTargets = new Dictionary<int, string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var match = LoadDirectiveRegex().Match(lines[i]);
            if (!match.Success) continue;

            var raw = match.Groups["path"].Value;
            loadTargets[i] = Path.IsPathRooted(raw) ? raw : Path.GetFullPath(Path.Combine(directory, raw));
        }

        return new ScriptNode(filePath, lines, loadTargets);
    }

    private static void CollectPackageDirectives(string filePath, IReadOnlyList<string> lines, ICollection<PackageDirective> target)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var match = NugetDirectiveRegex().Match(lines[i]);
            if (!match.Success) continue;

            var rawVersion = match.Groups["ver"].Success ? match.Groups["ver"].Value : null;
            target.Add(new PackageDirective(filePath, i + 1, lines[i], match.Groups["id"].Value.Trim(), NormalizeVersion(rawVersion)));
        }
    }

    private static Dictionary<string, string> BuildTempPathMap(string entryScript, IEnumerable<string> originalPaths)
    {
        var entryHash = ComputeShortHash(entryScript);
        var tempRoot = Path.Combine(TempCacheRoot, entryHash);
        Directory.CreateDirectory(tempRoot);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var original in originalPaths)
        {
            var name = Path.GetFileNameWithoutExtension(original);
            var pathHash = ComputeShortHash(original);
            map[original] = Path.Combine(tempRoot, $"{name}_{pathHash}.fsx");
        }

        return map;
    }

    private static string ComputeShortHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        return BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
    }

    private static string TransformLine(ScriptNode node, int index, IReadOnlyDictionary<string, string> pathMap)
    {
        var line = node.Lines[index];

        if (NugetDirectiveRegex().IsMatch(line))
            return $"// {line.Trim()} // resolved by FSharpNugetResolver";

        if (node.LoadTargets.TryGetValue(index, out var originalTarget) &&
            pathMap.TryGetValue(originalTarget, out var mappedTarget))
            return $"#load @\"{mappedTarget}\"";

        return line;
    }

    private static string? NormalizeVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // ReSharper disable once RedundantSuppressNullableWarningExpression
        var cleaned = VersionPrefixRegex().Replace(raw!.Trim(), "").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static Task<string[]> ReadAllLinesAsync(string path, CancellationToken ct)
    {
#if NET
        return File.ReadAllLinesAsync(path, ct);
#else
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(File.ReadAllLines(path));
#endif
    }

    private static Task WriteAllLinesAsync(string path, IEnumerable<string> lines, CancellationToken ct)
    {
#if NET
        return File.WriteAllLinesAsync(path, lines, ct);
#else
        ct.ThrowIfCancellationRequested();
        File.WriteAllLines(path, lines.ToArray());
        return Task.CompletedTask;
#endif
    }
}

internal sealed class TempFileCollection(IEnumerable<string> files) : IDisposable
{
    private readonly string[] _files = files.ToArray();

    public void Dispose()
    {
        foreach (var file in _files)
        {
            try { File.Delete(file); }
            catch { /* ignore */ }
        }
    }
}

internal readonly record struct PackageDirective(string FilePath, int LineNumber, string OriginalLine, string PackageId, string? Version);
internal readonly record struct ScriptNode(string Path, string[] Lines, IReadOnlyDictionary<int, string> LoadTargets);
internal readonly record struct LoadGraph(IReadOnlyDictionary<string, ScriptNode> Nodes, IReadOnlyList<PackageDirective> Packages);
internal readonly record struct RewriteResult(string EntryScriptPath, IDisposable Cleanup);
