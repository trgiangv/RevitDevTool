using System.Diagnostics;
using System.Text.RegularExpressions;
using DevTool.McpServer.RevitFileInfo;
using ModelContextProtocol.Protocol;

namespace DevTool.McpServer.Tools.Utils;

internal static partial class RevitLaunchCoordinator
{
    private const int DialogResolveSeconds = 90;

    private static readonly HashSet<string> SupportedLanguageCodes =
    [
        "ENU", "ENG", "FRA", "DEU", "ITA", "JPN", "KOR",
        "PLK", "ESP", "CHS", "CHT", "PTB", "RUS", "CSY", "HUN"
    ];

    public static (RevitLaunchContext? Context, CallToolResult? Error) Resolve(
        string? requestedVersion,
        string? languageCode,
        string? filePath,
        bool requireVersion)
    {
        if (requireVersion && string.IsNullOrWhiteSpace(requestedVersion))
            return (null, ToolHelpers.ErrorResult("versionNumber is required."));

        if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            return (null, ToolHelpers.ErrorResult($"File not found: {filePath}"));

        var version = ResolveVersion(requestedVersion, filePath);
        if (version is null)
            return (null, ToolHelpers.ErrorResult("No compatible Revit version found to open this file."));

        var revitPath = RevitPathResolver.FindRevitPath(version);
        if (string.IsNullOrWhiteSpace(revitPath))
            return (null, ToolHelpers.ErrorResult($"Revit {version} installation not found."));

        var language = string.IsNullOrWhiteSpace(languageCode)
            ? "ENU"
            : languageCode.Trim().ToUpperInvariant();
        if (!SupportedLanguageCodes.Contains(language))
            return (null, ToolHelpers.ErrorResult(
                $"Unsupported language code '{language}'. Supported values: {string.Join(", ", SupportedLanguageCodes)}"));

        var arguments = new List<string> { "/nosplash", "/language", language };
        if (!string.IsNullOrWhiteSpace(filePath))
            arguments.Add(filePath);

        return (new RevitLaunchContext(version, revitPath, language, arguments), null);
    }

    /// <summary>
    /// Determines the Revit version to launch:
    /// 1. If explicitly requested, use that.
    /// 2. If a file is provided, read its saved version, try exact match,
    ///    then the nearest higher installed version. Fail if none is >= file version.
    /// 3. Fall back to the latest installed version.
    /// </summary>
    private static string? ResolveVersion(string? requestedVersion, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
            return requestedVersion;

        var installedVersions = RevitPathResolver.GetInstalledVersions();
        if (installedVersions.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return installedVersions.FirstOrDefault();

        var fileVersion = ReadFileVersion(filePath);
        if (fileVersion is null || !int.TryParse(fileVersion, out var fileYear))
            return installedVersions.FirstOrDefault();

        return installedVersions
            .Where(v => int.TryParse(v, out var y) && y >= fileYear)
            .OrderBy(v => v)
            .FirstOrDefault();
    }

    private static string? ReadFileVersion(string filePath)
    {
        try
        {
            var info = BasicFileInfoReader.Read(filePath);
            if (info?.RevitVersion is null) return null;

            var match = RevitVersion().Match(info.RevitVersion);
            return match.Success ? match.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public static (Process? Process, CallToolResult? Error) StartProcess(RevitLaunchContext context)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = context.RevitPath,
                // Keep launched GUI process isolated from MCP stdio transport.
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(context.RevitPath) ?? Environment.CurrentDirectory,
            };
            foreach (var arg in context.Arguments)
                startInfo.ArgumentList.Add(arg);

            var process = Process.Start(startInfo);
            return process is null
                ? (null, ToolHelpers.ErrorResult("Failed to start Revit process."))
                : (process, null);
        }
        catch (Exception ex)
        {
            return (null, ToolHelpers.ErrorResult($"Failed to launch Revit: {ex.Message}"));
        }
    }

    public static void StartDialogResolver(int processId, CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DialogResolveSeconds));
            var options = new StartupDialogResolverOptions();
            try
            {
                await StartupDialogResolver.RunAsync(processId, options, TimeSpan.FromSeconds(DialogResolveSeconds), cts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when timer expires.
            }
        }, cancellationToken);
    }

    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex RevitVersion();
}
