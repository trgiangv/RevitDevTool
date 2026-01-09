using RevitDevTool.Models.Config;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// Factory for creating Serilog-based logger adapters.
/// Configures Serilog with appropriate sinks based on LogConfig settings.
/// </summary>
internal sealed class SerilogLoggerFactory : ILoggerFactory
{
    private readonly LoggingLevelSwitch _levelSwitch = new(LogEventLevel.Debug);

    public ILoggerAdapter CreateLogger(LogConfig config, ILogOutputSink? outputSink, bool isDarkTheme)
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .Enrich.FromLogContext();

        if (outputSink is RichTextBoxSink richTextBoxSink && !config.UseExternalFileOnly)
        {
            loggerConfig = richTextBoxSink.ConfigureSerilog(loggerConfig, isDarkTheme);
        }

        if (config.IsSaveLogEnabled)
        {
            loggerConfig = ConfigureFileSink(loggerConfig, config);
        }

        return new SerilogAdapter(loggerConfig.CreateLogger());
    }

    public void SetMinimumLevel(LogLevel level)
    {
        _levelSwitch.MinimumLevel = level switch
        {
            LogLevel.Verbose => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Debug
        };
    }

    private static LoggerConfiguration ConfigureFileSink(LoggerConfiguration config, LogConfig logConfig)
    {
        return logConfig.SaveFormat switch
        {
            LogSaveFormat.Json => config.WriteTo.File(
                formatter: new JsonFormatter(renderMessage: true),
                path: logConfig.FilePath,
                rollingInterval: logConfig.TimeInterval,
                shared: true),
            
            LogSaveFormat.Clef => config.WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: logConfig.FilePath,
                rollingInterval: logConfig.TimeInterval,
                shared: true),
            
            LogSaveFormat.Sqlite => config.WriteTo.SQLite(
                sqliteDbPath: logConfig.FilePath,
                tableName: "Logs",
                storeTimestampInUtc: true),
            
            _ => config.WriteTo.File(
                path: logConfig.FilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: logConfig.TimeInterval,
                shared: true)
        };
    }
}

