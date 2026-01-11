using System.IO;
using Microsoft.Extensions.Logging;
using RevitDevTool.Models.Config;
using ZLogger;
using MsLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory;

namespace RevitDevTool.Logging.ZLogger;

/// <summary>
/// Factory for creating ZLogger-based logger adapters.
/// Configures ZLogger with appropriate providers based on LogConfig settings.
/// </summary>
[UsedImplicitly]
internal sealed class ZLoggerLoggerFactory : ILoggerFactory
{
    private const int DefaultBufferCapacity = 10_000;
    private const int DefaultRollingSizeKb = 10 * 1024; // 10MB
    
    private Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;
    private LogLevel _minimumLevel = LogLevel.Debug;

    public ILoggerAdapter CreateLogger(LogConfig config, ILogOutputSink? outputSink, bool isDarkTheme)
    {
        // Dispose previous factory if exists
        _loggerFactory?.Dispose();

        _loggerFactory = MsLoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(_minimumLevel);

            // Add RichTextBox sink if available and not using external file only
            if (outputSink is ZloggerRichTextBoxSink richTextBoxSink && !config.UseExternalFileOnly)
            {
                richTextBoxSink.ConfigureZLogger(builder, isDarkTheme);
            }

            // Add file sink if save log is enabled
            if (config.IsSaveLogEnabled)
            {
                ConfigureFileSink(builder, config);
            }
        });

        var logger = _loggerFactory.CreateLogger("RevitDevTool");
        return new ZLoggerAdapter(logger, _loggerFactory);
    }

    public void SetMinimumLevel(LogLevel level)
    {
        _minimumLevel = level;
    }

    private static void ConfigureFileSink(ILoggingBuilder builder, LogConfig logConfig)
    {
        // SQLite not supported by ZLogger - fallback to Json format
        var saveFormat = logConfig.SaveFormat == LogSaveFormat.Sqlite 
            ? LogSaveFormat.Json 
            : logConfig.SaveFormat;
        
        var directory = Path.GetDirectoryName(logConfig.FilePath) ?? ".";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(logConfig.FilePath);
        var extension = Path.GetExtension(logConfig.FilePath);
        
        // Use RollingFile for time-based and size-based rotation (matching Serilog behavior)
        builder.AddZLoggerRollingFile(options =>
        {
            // File path pattern: {filename}_{date}_{sequence}.{ext}
            options.FilePathSelector = (timestamp, seq) =>
                Path.Combine(directory, $"{fileNameWithoutExt}_{timestamp.ToLocalTime():yyyyMMdd}_{seq:000}{extension}");
            options.RollingInterval = logConfig.TimeInterval.ToZlogger();
            options.RollingSizeKB = DefaultRollingSizeKb;
            options.FileShared = true;
            options.FullMode = BackgroundBufferFullMode.Drop;
            options.BackgroundBufferCapacity = DefaultBufferCapacity;
            
            // Configure formatter based on save format
            ConfigureFormatter(options, saveFormat);
        });
    }

    private static void ConfigureFormatter(ZLoggerOptions options, LogSaveFormat format)
    {
        switch (format)
        {
            case LogSaveFormat.Json:
                options.UseJsonFormatter();
                break;
            case LogSaveFormat.Clef:
                options.UseJsonFormatter(formatter =>
                {
                    formatter.IncludeProperties = IncludeProperties.Timestamp |
                                                  IncludeProperties.LogLevel |
                                                  IncludeProperties.Message |
                                                  IncludeProperties.ParameterKeyValues;
                });
                break;
            default: // Text format
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0:local-longdate} [{1:short}] ", FormatPrefix);
                });
                break;
        }
    }
    
    private static void FormatPrefix(in MessageTemplate template, in LogInfo info)
    {
        template.Format(info.Timestamp, info.LogLevel);
    }
}
