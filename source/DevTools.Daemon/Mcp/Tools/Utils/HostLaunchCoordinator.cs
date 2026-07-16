using System.Diagnostics;
using System.Text.RegularExpressions;
using DevTools.Logging;
using DevTools.Daemon.Mcp.RevitFileInfo;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Tools.Utils;

/// <summary>Resolves paths and arguments for launching host applications.</summary>
internal static partial class HostLaunchCoordinator
{
    public static (HostLaunchContext? Context, CallToolResult? Error) Resolve(
        HostApp hostApp,
        string? requestedVersion,
        string? languageCode,
        string? filePath,
        bool requireVersion)
    {
        if (requireVersion && string.IsNullOrWhiteSpace(requestedVersion))
            return (null, ToolHelpers.ErrorResult("versionNumber is required."));

        if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            return (null, ToolHelpers.ErrorResult($"File not found: {filePath}"));

        if (hostApp == HostApp.Revit)
            return ResolveRevit(requestedVersion, languageCode, filePath);

        if (hostApp.IsAcadFamily())
            return ResolveAcad(hostApp, requestedVersion, filePath);

        return (null, ToolHelpers.ErrorResult($"Launch not yet supported for {hostApp}."));
    }

    public static (Process? Process, CallToolResult? Error) StartProcess(HostLaunchContext context)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = context.ExePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(context.ExePath) ?? Environment.CurrentDirectory,
            };
            foreach (var arg in context.Arguments)
                startInfo.ArgumentList.Add(arg);

            var process = Process.Start(startInfo);
            return process is null
                ? (null, ToolHelpers.ErrorResult($"Failed to start {context.HostApp} process."))
                : (process, null);
        }
        catch (Exception ex)
        {
            return (null, ToolHelpers.ErrorResult($"Failed to launch {context.HostApp}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Starts a PID-scoped dialog resolver that outlives the MCP request.
    /// Only the pre-start check uses <paramref name="cancellationToken"/>; once running,
    /// the resolver uses an independent 90s deadline so remaining add-in dialogs keep
    /// being dismissed after the tool returns (agents often cancel the request CT then).
    /// </summary>
    public static Task<StartupDialogResolverResult>? StartDialogResolver(
        HostApp hostApp,
        int processId,
        CancellationToken cancellationToken)
    {
        if (hostApp != HostApp.Revit && !hostApp.IsAcadFamily()) return null;
        if (cancellationToken.IsCancellationRequested) return null;
        return ResolveDialogsAsync(processId);
    }

    /// <summary>
    /// Best-effort snapshot of resolver progress. Returns null if still running after
    /// <paramref name="wait"/> — the background resolver continues independently.
    /// </summary>
    public static async Task<StartupDialogResolverResult?> TryAwaitResolverResultAsync(
        Task<StartupDialogResolverResult>? task,
        TimeSpan wait)
    {
        if (task is null) return null;
        if (task.IsCompletedSuccessfully) return task.Result;
        try
        {
            using var cts = new CancellationTokenSource(wait);
            return await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch { return null; }
    }

    private static async Task<StartupDialogResolverResult> ResolveDialogsAsync(int processId)
    {
        // Independent of MCP request CT — must keep polling after tool response.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        try
        {
            return await StartupDialogResolver.RunAsync(
                processId,
                new StartupDialogResolverOptions(),
                TimeSpan.FromSeconds(90),
                cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new StartupDialogResolverResult(TimedOut: false, Events: []);
        }
    }

    #region Revit

    private static readonly HashSet<string> SupportedLanguageCodes =
    [
        "ENU", "ENG", "FRA", "DEU", "ITA", "JPN", "KOR",
        "PLK", "ESP", "CHS", "CHT", "PTB", "RUS", "CSY", "HUN"
    ];

    private static (HostLaunchContext? Context, CallToolResult? Error) ResolveRevit(
        string? requestedVersion, string? languageCode, string? filePath)
    {
        var version = ResolveRevitVersion(requestedVersion, filePath);
        if (version is null)
            return (null, ToolHelpers.ErrorResult("No compatible Revit version found."));

        var exePath = RevitPathResolver.FindRevitPath(version);
        if (string.IsNullOrWhiteSpace(exePath))
            return (null, ToolHelpers.ErrorResult($"Revit {version} installation not found."));

        var language = string.IsNullOrWhiteSpace(languageCode)
            ? "ENU"
            : languageCode.Trim().ToUpperInvariant();
        if (!SupportedLanguageCodes.Contains(language))
            return (null, ToolHelpers.ErrorResult(
                $"Unsupported language code '{language}'. Supported: {string.Join(", ", SupportedLanguageCodes)}"));

        var arguments = new List<string> { "/nosplash", "/language", language };
        if (!string.IsNullOrWhiteSpace(filePath))
            arguments.Add(filePath);

        return (new HostLaunchContext(HostApp.Revit, version, exePath, language, arguments), null);
    }

    private static string? ResolveRevitVersion(string? requestedVersion, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
            return requestedVersion;

        var installedVersions = RevitPathResolver.GetInstalledVersions();
        if (installedVersions.Count == 0) return null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return installedVersions.FirstOrDefault();

        var fileVersion = ReadRevitFileVersion(filePath);
        if (fileVersion is null || !int.TryParse(fileVersion, out var fileYear))
            return installedVersions.FirstOrDefault();

        return installedVersions
            .Where(v => int.TryParse(v, out var y) && y >= fileYear)
            .OrderBy(v => v)
            .FirstOrDefault();
    }

    private static string? ReadRevitFileVersion(string filePath)
    {
        try
        {
            using var file = RevitCompoundFile.Open(filePath);
            var info = BasicFileInfoReader.Read(file);
            if (info?.RevitVersion is null) return null;
            var match = RevitVersionPattern().Match(info.RevitVersion);
            return match.Success ? match.Value : null;
        }
        catch { return null; }
    }

    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex RevitVersionPattern();

    #endregion

    #region AutoCAD family

    private static (HostLaunchContext? Context, CallToolResult? Error) ResolveAcad(
        HostApp hostApp, string? requestedVersion, string? filePath)
    {
        var version = ResolveAcadVersion(requestedVersion);
        if (version is null)
            return (null, ToolHelpers.ErrorResult("No AutoCAD-family installation found."));

        var exePath = AcadPathResolver.FindAcadPath(version, hostApp);
        if (string.IsNullOrWhiteSpace(exePath))
            return (null, ToolHelpers.ErrorResult($"{hostApp} {version} installation not found."));

        var arguments = new List<string> { "/nologo" };
        if (!string.IsNullOrWhiteSpace(filePath))
            arguments.Add(filePath);

        return (new HostLaunchContext(hostApp, version, exePath, null, arguments), null);
    }

    private static string? ResolveAcadVersion(string? requestedVersion)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
            return requestedVersion;

        var installed = AcadPathResolver.GetInstalledVersions();
        return installed.FirstOrDefault();
    }

    #endregion
}

internal sealed record HostLaunchContext(
    HostApp HostApp,
    string Version,
    string ExePath,
    string? LanguageCode,
    IReadOnlyList<string> Arguments);
