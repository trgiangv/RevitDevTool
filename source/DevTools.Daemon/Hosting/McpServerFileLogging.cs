using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Daemon file logging: hourly ZLogger rolling under <c>%APPDATA%/RevitDevTool/mcp-server</c>.
/// File names: <c>log_{pid}_{yyyyMMddHH}_{seq}.log</c>. Shared by tray and stdio (PID separates processes).
/// </summary>
internal static class McpServerFileLogging
{
    private const string FolderName = "mcp-server";
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private static string LogFolder => Path.Combine(AppUtils.GetApplicationDataPath(), FolderName);

    public static void Configure(ILoggingBuilder logging, bool clearProviders)
    {
        var folder = LogFolder;
        Directory.CreateDirectory(folder);
        CleanupOldLogs(folder);

        if (clearProviders)
            logging.ClearProviders();

        var pid = Environment.ProcessId;
        logging.AddZLoggerRollingFile(
            (timestamp, seq) => Path.Combine(folder, $"log_{pid}_{timestamp:yyyyMMddHH}_{seq:D3}.log"),
            RollingInterval.Hour);

        logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
#if !DEBUG
        logging.AddFilter("Microsoft.Extensions.Hosting", LogLevel.None);
        logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
#endif
    }

    private static void CleanupOldLogs(string folder)
    {
        if (!Directory.Exists(folder))
            return;

        var cutoff = DateTime.UtcNow - Retention;
        foreach (var path in Directory.EnumerateFiles(folder, "log_*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup; locked/current files are skipped.
            }
        }
    }
}
