using DevTools.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace DevTools.Daemon.Composition;

internal static class FileLogging
{
    public static void Configure(ILoggingBuilder logging, IConfiguration configuration, bool clearProviders)
    {
        var options = configuration.GetSection(FileLogOptions.SectionName).Get<FileLogOptions>()
                      ?? new FileLogOptions();
        var folder = Path.Combine(AppUtils.GetApplicationDataPath(), options.Folder);
        Directory.CreateDirectory(folder);
        CleanupOldLogs(folder, TimeSpan.FromDays(options.RetentionDays));

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

    private static void CleanupOldLogs(string folder, TimeSpan retention)
    {
        if (!Directory.Exists(folder))
            return;

        var cutoff = DateTime.UtcNow - retention;
        foreach (var path in Directory.EnumerateFiles(folder, "log_*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                    File.Delete(path);
            }
            catch
            {
                /* locked/current files */
            }
        }
    }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed class FileLogOptions
{
    public const string SectionName = "Logging:File";

    public string Folder { get; set; } = "logs";
    public int RetentionDays { get; set; } = 30;
}
