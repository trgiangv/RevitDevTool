using Microsoft.Extensions.Logging;
using RevitDevTool.Models.Config;
using RevitDevTool.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.IO;

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

        if (outputSink is SerilogRichTextBoxSink richTextBoxSink && !config.UseExternalFileOnly)
        {
            loggerConfig = richTextBoxSink.ConfigureSerilog(loggerConfig, isDarkTheme, config.EnablePrettyJson);
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
}
