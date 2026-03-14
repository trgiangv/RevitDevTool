using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace RevitDevTool.Execution.Providers.Python;

public static class PythonEmbedded
{
    private const string ParserSourcePath = "RevitDevTool.Resources.scripts.Parser.py";
    private const string ToolParserSourcePath = "RevitDevTool.Resources.scripts.ToolParser.py";
    private const string ToolInvokeSourcePath = "RevitDevTool.Resources.scripts.ToolInvoke.py";
    private const string SetupSourcePath = "RevitDevTool.Resources.scripts.Setup.py";
    private const string ResetSourcePath = "RevitDevTool.Resources.scripts.Reset.py";
    private const string PixiTomlSourcePath = "RevitDevTool.Resources.scripts.pixi.toml";
    
    public static string ParserScriptPath => TryGetCached(ParserSourcePath, ScripPathCache);
    public static string PixiTomlPath => TryGetCached(PixiTomlSourcePath, ScripPathCache);
    public static string ToolInvokeScript => TryGetCached(ToolInvokeSourcePath, ScriptCache);
    public static string SetupScript => TryGetCached(SetupSourcePath, ScriptCache);
    public static string ResetScript => TryGetCached(ResetSourcePath, ScriptCache);
    public static string ToolParserScript => TryGetCached(ToolParserSourcePath, ScriptCache);

    private static readonly string[] CachePaths =
    [
        ToolParserSourcePath,
        ToolInvokeSourcePath,
        SetupSourcePath,
        ResetSourcePath
    ];
    
    private static readonly string[] CopyPaths =
    [
        ParserSourcePath,
        PixiTomlSourcePath
    ];
    
    private static readonly ConcurrentDictionary<string, string> ScriptCache = new();
    private static readonly ConcurrentDictionary<string, string> ScripPathCache = new();
    
    static PythonEmbedded()
    {
        EnsureCacheScripts();
        EnsureCopyScripts(PythonEnvironment.PixiProjectDir);
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
    
    private static void EnsureCopyScripts(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var path in CopyPaths)
        {
            try
            {
                using var stream = OpenResourceStreamByPath(path);
                var fileName = GetFileName(path);
                var targetPath = Path.Combine(targetDirectory, fileName);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                stream.CopyTo(fileStream);
                ScripPathCache[path] = targetPath;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to copy embedded script '{path}' to '{targetDirectory}': {ex.Message}");
            }
        }
    }
    
    private static string TryGetCached(string resourcePath, IDictionary<string, string> cache)
    {
        if (cache.TryGetValue(resourcePath, out var value))
            return value;
        
        EnsureCacheScripts();
        EnsureCopyScripts(PythonEnvironment.PixiProjectDir);
        return cache.TryGetValue(resourcePath, out var cachedValue)
            ? cachedValue
            : throw new InvalidOperationException($"Resource '{resourcePath}' was not found in cache after loading. Ensure it is embedded and loaded correctly.");
    }
    
    private static Stream OpenResourceStreamByPath(string resourcePath)
    {
        var stream = typeof(PythonEmbedded).Assembly.GetManifestResourceStream(resourcePath);
        return stream ?? throw new InvalidOperationException($"Embedded resource '{resourcePath}' was not found.");
    }

    private static string GetFileName(string resourcePath)
    {
        var parts = resourcePath.Split('.');
        return parts.Length < 2 
            ? throw new ArgumentOutOfRangeException(nameof(resourcePath), resourcePath, "Invalid embedded resource path.") 
            : $"{parts[^2]}.{parts[^1]}";
    }
}
