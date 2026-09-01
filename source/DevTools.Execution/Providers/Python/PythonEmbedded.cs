using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using DevTools.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

public static class PythonEmbedded
{
    /// <summary>
    /// Logical prefix for scripts embedded in DevTools.Execution. After ILRepack, manifests may be
    /// prefixed with the host assembly name, e.g. <c>AcadDevTool.DevTools.Execution.Resources.scripts.Parser.py</c>.
    /// </summary>
    private const string ExecutionScriptsPrefix = "DevTools.Execution.Resources.scripts";

    private static HostApp host;

    /// <summary>
    /// Configures host-specific setup script from <see cref="DevTools.Hosting.IHostAppInfo.Host"/>. Must run before any script access.
    /// </summary>
    public static void Configure(HostApp hostApp)
    {
        host = hostApp;
        ScriptCache.Clear();
        ScriptPathCache.Clear();
    }

    private static string ParserSourcePath => $"{ExecutionScriptsPrefix}.Parser.py";
    private static string ToolParserSourcePath => $"{ExecutionScriptsPrefix}.ToolParser.py";
    private static string ToolInvokeSourcePath => $"{ExecutionScriptsPrefix}.ToolInvoke.py";
    private static string PytestRunnerSourcePath => $"{ExecutionScriptsPrefix}.PytestRunner.py";
    private static string IpyTestDriverSourcePath => $"{ExecutionScriptsPrefix}.IpyTestDriver.py";

    private static string SetupSourcePath => host switch
    {
        HostApp.Revit => $"{ExecutionScriptsPrefix}.SetupRevit.py",
        _ => $"{ExecutionScriptsPrefix}.SetupAcad.py",
    };

    /// <summary>Short filename for stack traces (e.g. <c>SetupRevit.py</c>), matching <see cref="SetupSourcePath"/>.</summary>
    public static string SetupScriptFileName => GetFileName(SetupSourcePath);

    private static string ResetSourcePath => $"{ExecutionScriptsPrefix}.Reset.py";
    private static string PixiTomlSourcePath => $"{ExecutionScriptsPrefix}.pixi.toml";

    public static string ParserScriptPath => TryGetCached(ParserSourcePath, ScriptPathCache);
    public static string PixiTomlPath => TryGetCached(PixiTomlSourcePath, ScriptPathCache);
    public static string IpyTestDriverScriptPath => TryGetCached(IpyTestDriverSourcePath, ScriptPathCache);
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

    private static string[] CopyPaths =>
    [
        ParserSourcePath,
        PixiTomlSourcePath,
        IpyTestDriverSourcePath,
    ];

    private static readonly ConcurrentDictionary<string, string> ScriptCache = new();
    private static readonly ConcurrentDictionary<string, string> ScriptPathCache = new();

    private static bool IsCacheReady => CachePaths.All(ScriptCache.ContainsKey);
    private static bool IsCopyReady => CopyPaths.All(ScriptPathCache.ContainsKey);

    public static void EnsureExtracted(ILogger? logger = null)
    {
        if (IsCacheReady && IsCopyReady) return;

        if (!IsCacheReady)
            EnsureCacheScripts(logger);

        var pixiEnvDir = PixiEnvironmentProvider.PixiProjectDir;
        Directory.CreateDirectory(pixiEnvDir);

        // Parser must be overwritten on every load to ensure latest version is used.
        CopyResource(ParserSourcePath, pixiEnvDir, overwrite: true, logger);
        CopyResource(IpyTestDriverSourcePath, pixiEnvDir, overwrite: true, logger);

        // Do not override to prevent re-install package already installed from previous session
        CopyResource(PixiTomlSourcePath, pixiEnvDir, overwrite: false, logger);

        // https://pixi.prefix.dev/latest/reference/pixi_configuration/#tls-no-verify
        // run once for each session, Required for corporate environments with custom CA certificates.
        SetupPixiConfig(logger);
    }

    private static void SetupPixiConfig(ILogger? logger = null)
    {
        try
        {
            var pixiConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".pixi"
            );

            Directory.CreateDirectory(pixiConfigDir);

            const string rootCertsSystem = "tls-root-certs = \"system\"";
            var configPath = Path.Combine(pixiConfigDir, "config.toml");

            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, rootCertsSystem + Environment.NewLine);
                return;
            }

            var lines = File.ReadAllLines(configPath).ToList();

            var tlsLineIndex = lines.FindIndex(line =>
                line.TrimStart().TrimEnd().StartsWith("tls-root-certs", StringComparison.OrdinalIgnoreCase));

            if (tlsLineIndex >= 0)
            {
                lines[tlsLineIndex] = rootCertsSystem;
            }
            else
            {
                lines.Add(rootCertsSystem);
            }

            File.WriteAllLines(configPath, lines);
        }
        catch (Exception ex)
        {
            logger?.ZLogError($"Failed to setup Pixi user config: {ex.Message}");
        }
    }

    private static void EnsureCacheScripts(ILogger? logger = null)
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
                logger?.ZLogError($"Failed to load embedded script '{path}': {ex.Message}");
            }
        }
    }

    private static void CopyResource(string resourcePath, string targetDirectory, bool overwrite, ILogger? logger = null)
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
            logger?.ZLogError($"Failed to copy embedded resource '{resourcePath}': {ex.Message}");
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
