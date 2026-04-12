using System.Diagnostics;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.McpServer.Tools.Utils;

internal static class RevitLaunchCoordinator
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

        var version = string.IsNullOrWhiteSpace(requestedVersion)
            ? RevitPathResolver.GetInstalledVersions().FirstOrDefault()
            : requestedVersion;
        if (string.IsNullOrWhiteSpace(version))
            return (null, ToolHelpers.ErrorResult("No connected instance and no installed Revit version found to launch."));

        if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            return (null, ToolHelpers.ErrorResult($"File not found: {filePath}"));

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
}
