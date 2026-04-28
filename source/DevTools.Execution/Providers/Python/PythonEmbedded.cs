using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
namespace DevTools.Execution.Providers.Python;

public static class PythonEmbedded
{
    private static Assembly? _resourceAssembly;
    private static string _resourcePrefix = "RevitDevTool.Resources.scripts";

    /// <summary>
    /// Configures which assembly and resource prefix to use for embedded Python scripts.
    /// Must be called before any script access (typically at host startup).
    /// </summary>
    public static void Configure(Assembly resourceAssembly, string resourcePrefix)
    {
        _resourceAssembly = resourceAssembly;
        _resourcePrefix = resourcePrefix;
        ScriptCache.Clear();
        ScriptPathCache.Clear();
    }

    private static string ParserSourcePath => $"{_resourcePrefix}.Parser.py";
    private static string ToolParserSourcePath => $"{_resourcePrefix}.ToolParser.py";
    private static string ToolInvokeSourcePath => $"{_resourcePrefix}.ToolInvoke.py";
    private static string PytestRunnerSourcePath => $"{_resourcePrefix}.PytestRunner.py";
    private static string SetupSourcePath => $"{_resourcePrefix}.Setup.py";
    private static string ResetSourcePath => $"{_resourcePrefix}.Reset.py";
    private static string PixiTomlSourcePath => $"{_resourcePrefix}.pixi.toml";
    
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
        var assembly = _resourceAssembly ?? typeof(PythonEmbedded).Assembly;
        var stream = assembly.GetManifestResourceStream(resourcePath);
        return stream ?? throw new InvalidOperationException($"Embedded resource '{resourcePath}' was not found in assembly '{assembly.GetName().Name}'.");
    }

    private static string GetFileName(string resourcePath)
    {
        var parts = resourcePath.Split('.');
        return parts.Length < 2 
            ? throw new ArgumentOutOfRangeException(nameof(resourcePath), resourcePath, @"Invalid embedded resource path.") 
            : $"{parts[^2]}.{parts[^1]}";
    }
}
