using Cysharp.Text;
using Microsoft.Extensions.Logging;
using RevitDevTool.Models.Config;
using RevitDevTool.Utils;
using System.IO;
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
        _loggerFactory = MsLoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(_minimumLevel);

            if (outputSink is ZloggerRichTextBoxSink richTextBoxSink && !config.UseExternalFileOnly)
            {
                richTextBoxSink.ConfigureZLogger(builder, isDarkTheme);
            }

            if (config.IsSaveLogEnabled)
            {
                ConfigureFileSink(builder, config);
            }
        });

        var logger = _loggerFactory.CreateLogger("RevitDevTool");
        return new ZLoggerAdapter(logger);
    }

    public void SetMinimumLevel(LogLevel level)
    {
        _minimumLevel = level;
    }

    private static void ConfigureFileSink(ILoggingBuilder builder, LogConfig logConfig)
    {
        var saveFormat = logConfig.SaveFormat;
        var extension = logConfig.SaveFormat.ToFileExtension();
        var directory = logConfig.LogFolder;

        builder.AddZLoggerRollingFile(options =>
        {
            options.FilePathSelector = (timestamp, seq) =>
            {
                using var sb = ZString.CreateStringBuilder();
                sb.Append(directory);
                sb.Append(Path.DirectorySeparatorChar);
                sb.Append("log_");
                sb.Append(timestamp.ToLocalTime().ToString("yyyyMMdd"));
                sb.Append('_');
                sb.AppendFormat("{0:000}", seq);
                sb.Append('.');
                sb.Append(extension);
                return sb.ToString();
            };
            options.RollingInterval = logConfig.TimeInterval.ToZlogger();
            options.RollingSizeKB = DefaultRollingSizeKb;
            options.FileShared = true;
            options.FullMode = BackgroundBufferFullMode.Block;
            options.BackgroundBufferCapacity = DefaultBufferCapacity;
            ConfigureFormatter(options, saveFormat);
        });
    }

    private static void ConfigureFormatter(ZLoggerOptions options, LogSaveFormat format)
    {
        switch (format)
        {
            case LogSaveFormat.Json:
                options.UseJsonFormatter(formatter =>
                {
                    formatter.IncludeProperties = IncludeProperties.Timestamp |
                                                  IncludeProperties.LogLevel |
                                                  IncludeProperties.Message |
                                                  IncludeProperties.ParameterKeyValues;
                });
                break;
            case LogSaveFormat.Text:
            default:
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
