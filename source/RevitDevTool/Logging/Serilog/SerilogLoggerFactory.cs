using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings.Config;
using RevitDevTool.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.IO;
using RevitEnricher = RevitDevTool.Logging.Enums.RevitEnricher;

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// Factory for creating Serilog-based logger adapters.
/// Configures Serilog with appropriate sinks based on LogConfig settings.
/// </summary>
[UsedImplicitly]
internal sealed class SerilogLoggerFactory : ILoggerFactory
{
    private readonly LoggingLevelSwitch _levelSwitch = new(LogEventLevel.Debug);

    public ILoggerAdapter CreateLogger(LogConfig config, ILogOutputSink? outputSink, bool isDarkTheme)
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .Enrich.FromLogContext();

        loggerConfig = ConfigureRevitEnrichers(loggerConfig, config.RevitEnrichers);

        if (outputSink is SerilogRichTextBoxSink richTextBoxSink && !config.UseExternalFileOnly)
        {
            loggerConfig = richTextBoxSink.ConfigureSerilog(
                loggerConfig,
                isDarkTheme,
                config.EnablePrettyJson,
                config.IncludeStackTrace);
        }

        if (config.IsSaveLogEnabled)
        {
            loggerConfig = ConfigureFileSink(loggerConfig, config);
        }

        return new SerilogAdapter(loggerConfig.CreateLogger());
    }

    public void SetMinimumLevel(LogLevel level)
    {
        _levelSwitch.MinimumLevel = level.ToSerilog();
    }

    private static LoggerConfiguration ConfigureFileSink(LoggerConfiguration config, LogConfig logConfig)
    {
        var extension = logConfig.SaveFormat.ToFileExtension();
        var logFilePath = Path.Combine(logConfig.LogFolder, $"log.{extension}");

        return logConfig.SaveFormat switch
        {
            LogSaveFormat.Json => config.WriteTo.File(
                formatter: new JsonFormatter(renderMessage: true),
                path: logFilePath,
                shared: true,
                rollingInterval: logConfig.TimeInterval.ToSerilog()),
            _ => config.WriteTo.File(
                path: logFilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true,
                rollingInterval: logConfig.TimeInterval.ToSerilog())
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
