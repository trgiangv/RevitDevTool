using System.IO;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings.Config;
using RevitDevTool.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using ILogger = Serilog.ILogger;

namespace RevitDevTool.Logging;

/// <summary>
/// Factory for creating Serilog loggers.
/// Configures Serilog with appropriate sinks based on LogConfig settings.
/// </summary>
[UsedImplicitly]
public sealed class LoggerFactory
{
    private readonly LoggingLevelSwitch _levelSwitch = new(LogEventLevel.Debug);

    public ILogger CreateLogger(LogConfig config, ILoggingMonitor? monitor, bool isDarkTheme)
    {
        var hostConfig = config ?? throw new InvalidOperationException("Host logging requires RevitDevTool.Settings.Config.LogConfig.");
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .Enrich.FromLogContext();

        loggerConfig = ConfigureRevitEnrichers(loggerConfig, hostConfig.RevitEnrichers);

        if (monitor is RichTextBoxMonitor richTextBoxMonitor && !hostConfig.UseExternalFileOnly)
        {
            loggerConfig = richTextBoxMonitor.ConfigureSerilog(
                loggerConfig,
                isDarkTheme,
                hostConfig.EnablePrettyJson,
                hostConfig.IncludeStackTrace);
        }

        if (hostConfig.IsSaveLogEnabled)
        {
            loggerConfig = ConfigureFileSink(loggerConfig, hostConfig);
        }

        return loggerConfig.CreateLogger();
    }

    public void SetMinimumLevel(LogEventLevel level)
    {
        _levelSwitch.MinimumLevel = level;
    }

    private static LoggerConfiguration ConfigureFileSink(LoggerConfiguration config, LogConfig logConfig)
    {
        var extension = logConfig.SaveFormat == SaveFormat.Json ? "json" : "log";
        var pid = SettingsUtils.CurrentProcessId;
        var logFilePath = Path.Combine(logConfig.LogFolder, $"log_{pid}_.{extension}");

        return logConfig.SaveFormat switch
        {
            SaveFormat.Json => config.WriteTo.File(
                formatter: new JsonFormatter(renderMessage: true),
                path: logFilePath,
                shared: true,
                rollingInterval: logConfig.TimeInterval),
            _ => config.WriteTo.File(
                path: logFilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true,
                rollingInterval: logConfig.TimeInterval)
        };
    }

    private static LoggerConfiguration ConfigureRevitEnrichers(LoggerConfiguration config, RevitEnricher enrichers)
    {
        if (enrichers == RevitEnricher.None)
            return config;

        var uiApp = Context.UiApplication;
        if (uiApp == null!)
            return config;

        if (enrichers.HasFlag(RevitEnricher.RevitVersion))
            config = config.Enrich.WithRevitVersion(uiApp);

        if (enrichers.HasFlag(RevitEnricher.RevitBuild))
            config = config.Enrich.WithRevitBuild(uiApp);

        if (enrichers.HasFlag(RevitEnricher.RevitUserName))
            config = config.Enrich.WithRevitUserName(uiApp);

        if (enrichers.HasFlag(RevitEnricher.RevitLanguage))
            config = config.Enrich.WithRevitLanguage(uiApp);

        if (enrichers.HasFlag(RevitEnricher.RevitDocumentTitle))
            config = config.Enrich.WithRevitDocumentTitle(uiApp);

        if (enrichers.HasFlag(RevitEnricher.RevitDocumentPathName))
            config = config.Enrich.WithRevitDocumentPathName(uiApp);

        if (enrichers.HasFlag(RevitEnricher.RevitDocumentModelPath))
            config = config.Enrich.WithRevitDocumentModelPath(uiApp);

        return config;
    }
}
