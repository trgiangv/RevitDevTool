using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using DevTools.Logging;
namespace DevTools.Execution.Providers.Python;

public static class PythonEmbedded
{
    /// <summary>
    /// Logical prefix for scripts embedded in DevTools.Execution. After ILRepack, manifests may be
    /// prefixed with the host assembly name, e.g. <c>AcadDevTool.DevTools.Execution.Resources.scripts.Parser.py</c>.
    /// </summary>
    private const string ExecutionScriptsPrefix = "DevTools.Execution.Resources.scripts";

    private static HostApp _host;

    /// <summary>
    /// Configures host-specific setup script from <see cref="DevTools.Logging.IHostAppInfo.Host"/>. Must run before any script access.
    /// </summary>
    public static void Configure(HostApp host)
    {
        _host = host;
        ScriptCache.Clear();
        ScriptPathCache.Clear();
    }

    private static string ParserSourcePath => $"{ExecutionScriptsPrefix}.Parser.py";
    private static string ToolParserSourcePath => $"{ExecutionScriptsPrefix}.ToolParser.py";
    private static string ToolInvokeSourcePath => $"{ExecutionScriptsPrefix}.ToolInvoke.py";
    private static string PytestRunnerSourcePath => $"{ExecutionScriptsPrefix}.PytestRunner.py";

    private static string SetupSourcePath =>
        _host switch
        {
            HostApp.Revit => $"{ExecutionScriptsPrefix}.SetupRevit.py",
            _ => $"{ExecutionScriptsPrefix}.SetupAcad.py",
        };

    private static string ResetSourcePath => $"{ExecutionScriptsPrefix}.Reset.py";
    private static string PixiTomlSourcePath => $"{ExecutionScriptsPrefix}.pixi.toml";

    public static string ParserScriptPath => TryGetCached(ParserSourcePath, ScriptPathCache);
    public static string PixiTomlPath => TryGetCached(PixiTomlSourcePath, ScriptPathCache);
    public static string ToolInvokeScript => TryGetCached(ToolInvokeSourcePath, ScriptCache);
    public static string PytestRunnerScript => TryGetCached(PytestRunnerSourcePath, ScriptCache);
    public static string SetupScript => TryGetCached(SetupSourcePath, ScriptCache);
    public static string ResetScript => TryGetCached(ResetSourcePath, ScriptCache);
    public static string ToolParserScript => TryGetCached(ToolParserSourcePath, ScriptCache);

    private static string[] CachePaths =>
    [
        ToolParserSourcePath,
        ToolInvokeSourcePath,
        PytestRunnerSourcePath,
        SetupSourcePath,
        ResetSourcePath
    ];

    private static string[] AlwaysOverwritePaths =>
    [
        ParserSourcePath
    ];

    private static string[] CreateOnlyPaths =>
    [
        PixiTomlSourcePath
    ];

    private static readonly ConcurrentDictionary<string, string> ScriptCache = new();
    private static readonly ConcurrentDictionary<string, string> ScriptPathCache = new();

    private static bool IsCacheReady => CachePaths.All(ScriptCache.ContainsKey);
    private static bool IsCopyReady => AlwaysOverwritePaths.Concat(CreateOnlyPaths).All(ScriptPathCache.ContainsKey);

    public static void EnsureExtracted()
    {
        if (IsCacheReady && IsCopyReady) return;

        if (!IsCacheReady)
            EnsureCacheScripts();

        var targetDir = PixiEnvironmentProvider.PixiProjectDir;
        Directory.CreateDirectory(targetDir);

        foreach (var path in AlwaysOverwritePaths)
            CopyResource(path, targetDir, overwrite: true);

        foreach (var path in CreateOnlyPaths)
            CopyResource(path, targetDir, overwrite: false);
    }

    private static void EnsureCacheScripts()
    {
        foreach (var path in CachePaths)
        {
            if (ScriptCache.ContainsKey(path)) continue;
            try
            {
                using var stream = OpenResourceStreamByPath(path);
                using var reader = new StreamReader(stream);
                ScriptCache[path] = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to load embedded script '{path}': {ex.Message}");
            }
        }
    }

    private static void CopyResource(string resourcePath, string targetDirectory, bool overwrite)
    {
        try
        {
            var fileName = GetFileName(resourcePath);
            var targetPath = Path.Combine(targetDirectory, fileName);

            if (!overwrite && File.Exists(targetPath))
            {
                ScriptPathCache[resourcePath] = targetPath;
                return;
            }

            using var stream = OpenResourceStreamByPath(resourcePath);
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fileStream);

            ScriptPathCache[resourcePath] = targetPath;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to copy embedded resource '{resourcePath}': {ex.Message}");
        }
    }

    private static string TryGetCached(string resourcePath, ConcurrentDictionary<string, string> cache)
    {
        if (cache.TryGetValue(resourcePath, out var value))
            return value;

        EnsureExtracted();
        return cache.TryGetValue(resourcePath, out var cachedValue)
            ? cachedValue
            : throw new InvalidOperationException($"Resource '{resourcePath}' was not found in cache after loading. Ensure it is embedded and loaded correctly.");
    }

    private static Stream OpenResourceStreamByPath(string resourcePath)
    {
        var assembly = typeof(PythonEmbedded).Assembly;

        foreach (var candidate in EnumerateManifestResourceCandidates(assembly, resourcePath))
        {
            var stream = assembly.GetManifestResourceStream(candidate);
            if (stream != null)
                return stream;
        }

        throw new InvalidOperationException(
            $"Embedded resource '{resourcePath}' was not found in assembly '{assembly.GetName().Name}'. " +
            $"Tried primary name and '{assembly.GetName().Name}.{ExecutionScriptsPrefix}.*' fallback.");
    }

    /// <summary>
    /// Resolves manifest names across DevTools.Execution.dll and ILRepack-merged host layouts.
    /// </summary>
    private static IEnumerable<string> EnumerateManifestResourceCandidates(Assembly assembly, string resourcePath)
    {
        yield return resourcePath;

        var hostName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(hostName))
            yield break;

        var fileName = GetFileName(resourcePath);
        yield return $"{hostName}.{ExecutionScriptsPrefix}.{fileName}";

        var suffix = $".{ExecutionScriptsPrefix}.{fileName}";
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                yield return name;
        }
    }

    private static string GetFileName(string resourcePath)
    {
        var parts = resourcePath.Split('.');
        return parts.Length < 2
            ? throw new ArgumentOutOfRangeException(nameof(resourcePath), resourcePath, @"Invalid embedded resource path.")
            : $"{parts[^2]}.{parts[^1]}";
    }
}
