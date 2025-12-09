using RevitDevTool.Models.Config;
using Serilog.Events;
// ReSharper disable ConvertToExtensionBlock
namespace RevitDevTool.Utils;

using System.Globalization;
using System.Windows.Forms;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Json;
using Serilog.Formatting.Compact;
using Serilog.Sinks.RichTextBoxForms;

public static class LoggerConfigUtils
{
    /// <summary>
    ///     Adds a RichTextBox sink to the logger configuration
    /// </summary>
    /// <param name="config">Logger configuration</param>
    /// <param name="richTextBox">WinForms RichTextBox control</param>
    /// <param name="textTheme">RichTextBox text theme</param>
    /// <param name="sink">RichTextBox sink reference</param>
    /// <param name="maxLogLines">Maximum number of log lines to keep in the RichTextBox</param>
    /// <param name="autoScroll">Whether to auto-scroll to the latest log entry</param>
    /// <returns></returns>
    private static LoggerConfiguration WithRichTextBox(
        this LoggerConfiguration config,
        RichTextBox richTextBox,
        Serilog.Sinks.RichTextBoxForms.Themes.Theme textTheme,
        out RichTextBoxSink sink,
        int maxLogLines = 1000,
        bool autoScroll = true)
    {
        return config.WriteTo.RichTextBox(
            richTextBox,
            out sink,
            maxLogLines: maxLogLines,
            formatProvider: CultureInfo.InvariantCulture,
            theme: textTheme,
            prettyPrintJson: true,
            autoScroll: autoScroll
        );
    }
    
    /// <summary>
    /// Append a human-readable plain text log file sink.
    /// Example output: 23:10:15 [INF] Message text
    /// </summary>
    private static void WithBasicTextFile(
        this LoggerConfiguration config, 
        string path, 
        RollingInterval interval = RollingInterval.Day, 
        bool rollOnFileSizeLimit = false, 
        long fileSizeLimitBytes = 0, 
        bool shared = true, 
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        const string template =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        config.WriteTo.File(
            path: path,
            outputTemplate: template,
            rollingInterval: interval,
            fileSizeLimitBytes: fileSizeLimitBytes > 0 ? fileSizeLimitBytes : null,
            rollOnFileSizeLimit: rollOnFileSizeLimit,
            restrictedToMinimumLevel: restrictedToMinimumLevel,
            shared: shared);
    }
    
    /// <summary>
    ///     Adds a JSON file sink to the logger configuration
    /// </summary>
    /// <param name="config">Logger configuration</param>
    /// <param name="path">path to the log file</param>
    /// <param name="rollingInterval">log rolling interval</param>
    /// <param name="shared">whether the log file is shared between multiple processes</param>
    /// <param name="renderMessage">whether to render the message template into the "Message" property</param>
    /// <returns></returns>
    private static void WithJsonFile(
        this LoggerConfiguration config,
        string path,
        RollingInterval rollingInterval = RollingInterval.Day,
        bool shared = true,
        bool renderMessage = true)
    {
         config.WriteTo.File(
            formatter: new JsonFormatter(renderMessage: renderMessage),
            path: path,
            rollingInterval: rollingInterval,
            shared: shared
        );
    }

    /// <summary>
    ///     Adds a CLEF (Compact Log Event Format) file sink to the logger configuration
    ///     that produces structured log files suitable for analysis with tools like Seq
    /// </summary>
    /// <param name="config">Logger configuration</param>
    /// <param name="path">path to the log file</param>
    /// <param name="rollingInterval">log rolling interval</param>
    /// <param name="shared">whether the log file is shared between multiple processes</param>
    /// <returns>LoggerConfiguration</returns>
    private static void WithClefFile(this LoggerConfiguration config, string path, RollingInterval rollingInterval = RollingInterval.Day, bool shared = true)
    {
        config.WriteTo.File(
            formatter: new CompactJsonFormatter(),
            path: path,
            rollingInterval: rollingInterval,
            shared: shared
        );
    }

    /// <summary>
    ///    Adds a SQLite sink to the logger configuration
    /// </summary>
    /// <param name="config">Logger configuration</param>
    /// <param name="sqlitePath">path to the SQLite database file</param>
    /// <param name="tableName">name of the table to store logs</param>
    /// <param name="storeTimestampInUtc">whether to store timestamps in UTC</param>
    /// <returns></returns>
    private static void WithSqLite(this LoggerConfiguration config, string sqlitePath, string tableName = "Logs", bool storeTimestampInUtc = true)
    {
        config.WriteTo.SQLite(
            sqliteDbPath: sqlitePath,
            tableName: tableName,
            storeTimestampInUtc: storeTimestampInUtc
        );
    }
    
    /// <summary>
    /// Creates a default logger configuration with a RichTextBox sink
    /// </summary>
    /// <param name="loggingLevelSwitch">level switch to control logging level</param>
    /// <param name="richTextBox">WinForms RichTextBox control</param>
    /// <param name="textTheme">RichTextBox text theme</param>
    /// <param name="logConfig">log configuration</param>
    /// <param name="sink">RichTextBox sink reference</param>
    /// <returns>LoggerConfiguration</returns>
    public static LoggerConfiguration BuildLoggerConfiguration(
        LoggingLevelSwitch loggingLevelSwitch,
        RichTextBox richTextBox,
        Serilog.Sinks.RichTextBoxForms.Themes.Theme textTheme,
        LogConfig logConfig,
        out RichTextBoxSink sink)
    {
        var defaultConfig =  new LoggerConfiguration()
                .MinimumLevel.ControlledBy(loggingLevelSwitch)
                .Enrich.FromLogContext()
                .WithRichTextBox(richTextBox, textTheme, out sink);
        
        if (!logConfig.IsSaveLogEnabled) return defaultConfig;
        
        switch (logConfig.SaveFormat)
        {
            case LogSaveFormat.Json:
                defaultConfig.WithJsonFile(logConfig.FilePath, logConfig.TimeInterval);
                break;
            case LogSaveFormat.Clef:
                defaultConfig.WithClefFile(logConfig.FilePath, rollingInterval: logConfig.TimeInterval);
                break;
            case LogSaveFormat.Sqlite:
                defaultConfig.WithSqLite(logConfig.FilePath);
                break;
            case LogSaveFormat.Text:
                defaultConfig.WithBasicTextFile(logConfig.FilePath, interval: logConfig.TimeInterval);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        return defaultConfig;
    }
}
