using System.Drawing;
using System.Text;
using RevitDevTool.Scintilla.Contracts;

namespace RevitDevTool.Scintilla.Core;

public sealed class DefaultLogRenderStrategy : ILogRenderStrategy
#if NET8_0_OR_GREATER
    , IUtf8LogRenderStrategy
#endif
{
    public string FormatLine(LogEntry entry)
    {
        var builder = new StringBuilder(256);
        builder.Append('[');
        builder.Append(entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"));
        builder.Append(" ");
        builder.Append(GetShortLevel(entry.Level));
        builder.Append("] ");

        if (!string.IsNullOrWhiteSpace(entry.Source))
        {
            builder.Append(entry.Source);
            builder.Append(": ");
        }

        builder.Append(entry.Message);

        if (!string.IsNullOrWhiteSpace(entry.ExceptionText))
        {
            builder.AppendLine();
            builder.Append(entry.ExceptionText);
        }

        return builder.ToString();
    }

    public int GetStyleId(LogSeverity severity) => severity switch
    {
        LogSeverity.Trace => 10,
        LogSeverity.Debug => 11,
        LogSeverity.Information => 12,
        LogSeverity.Warning => 13,
        LogSeverity.Error => 14,
        LogSeverity.Critical => 15,
        _ => 12
    };

    public void ConfigureStyles(IStyleWriter styleWriter)
    {
        styleWriter.SetDefaultStyle("Cascadia Mono", 10, Color.Gainsboro, Color.FromArgb(30, 30, 30));
        styleWriter.SetStyle(10, Color.DarkGray, Color.FromArgb(30, 30, 30));
        styleWriter.SetStyle(11, Color.LightGray, Color.FromArgb(30, 30, 30));
        styleWriter.SetStyle(12, Color.Gainsboro, Color.FromArgb(30, 30, 30));
        styleWriter.SetStyle(13, Color.Gold, Color.FromArgb(30, 30, 30), bold: true);
        styleWriter.SetStyle(14, Color.IndianRed, Color.FromArgb(30, 30, 30), bold: true);
        styleWriter.SetStyle(15, Color.White, Color.Firebrick, bold: true);
    }

    private static string GetShortLevel(LogSeverity severity) => severity switch
    {
        LogSeverity.Trace => "TRC",
        LogSeverity.Debug => "DBG",
        LogSeverity.Information => "INF",
        LogSeverity.Warning => "WRN",
        LogSeverity.Error => "ERR",
        LogSeverity.Critical => "CRT",
        _ => "INF"
    };

#if NET8_0_OR_GREATER
    private static ReadOnlySpan<byte> PrefixOpen => "["u8;
    private static ReadOnlySpan<byte> PrefixClose => "] "u8;
    private static ReadOnlySpan<byte> SourceSuffix => ": "u8;
    private static readonly byte[] NewLine = Encoding.UTF8.GetBytes(Environment.NewLine);
    private static ReadOnlySpan<byte> Trace => "TRC"u8;
    private static ReadOnlySpan<byte> Debug => "DBG"u8;
    private static ReadOnlySpan<byte> Information => "INF"u8;
    private static ReadOnlySpan<byte> Warning => "WRN"u8;
    private static ReadOnlySpan<byte> Error => "ERR"u8;
    private static ReadOnlySpan<byte> Critical => "CRT"u8;

    public int GetLineUtf8ByteCount(LogEntry entry)
    {
        // [HH:mm:ss.fff LVL] + source + message + optional '\n' + exception
        var total = 18;

        if (!string.IsNullOrWhiteSpace(entry.Source))
            total += Encoding.UTF8.GetByteCount(entry.Source) + 2;

        total += Encoding.UTF8.GetByteCount(entry.Message);

        if (!string.IsNullOrWhiteSpace(entry.ExceptionText))
            total += 1 + Encoding.UTF8.GetByteCount(entry.ExceptionText);

        return total;
    }

    public int WriteLineUtf8(LogEntry entry, Span<byte> destination)
    {
        var written = 0;

        PrefixOpen.CopyTo(destination.Slice(written));
        written += PrefixOpen.Length;

        Span<char> tsBuffer = stackalloc char[12];
        entry.TimestampUtc.ToLocalTime().TryFormat(tsBuffer, out var tsCharsWritten, "HH:mm:ss.fff");
        written += Encoding.UTF8.GetBytes(tsBuffer.Slice(0, tsCharsWritten), destination.Slice(written));

        destination[written++] = (byte)' ';
        var level = GetShortLevelUtf8(entry.Level);
        level.CopyTo(destination.Slice(written));
        written += level.Length;

        PrefixClose.CopyTo(destination.Slice(written));
        written += PrefixClose.Length;

        if (!string.IsNullOrWhiteSpace(entry.Source))
        {
            written += Encoding.UTF8.GetBytes(entry.Source, destination.Slice(written));
            SourceSuffix.CopyTo(destination.Slice(written));
            written += SourceSuffix.Length;
        }

        written += Encoding.UTF8.GetBytes(entry.Message, destination.Slice(written));

        if (!string.IsNullOrWhiteSpace(entry.ExceptionText))
        {
            NewLine.CopyTo(destination.Slice(written));
            written += NewLine.Length;
            written += Encoding.UTF8.GetBytes(entry.ExceptionText, destination.Slice(written));
        }

        return written;
    }

    private static ReadOnlySpan<byte> GetShortLevelUtf8(LogSeverity severity) => severity switch
    {
        LogSeverity.Trace => Trace,
        LogSeverity.Debug => Debug,
        LogSeverity.Information => Information,
        LogSeverity.Warning => Warning,
        LogSeverity.Error => Error,
        LogSeverity.Critical => Critical,
        _ => Information
    };
#endif
}
