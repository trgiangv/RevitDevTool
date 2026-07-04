using DevTools.Logging.Options;
using ZLogger;
namespace DevTools.Logging.Extensions;

internal static class LogFormatterExtensions
{
    internal static void ConfigureFormatter(this ZLoggerOptions options, SaveFormat format)
    {
        if (format == SaveFormat.Json)
        {
            options.UseJsonFormatter();
            return;
        }

        options.UsePlainTextFormatter(formatter =>
            formatter.SetPrefixFormatter(
                $"[{0:local-timeonly} {1:short}]{2} ",
                (in t, in i) =>
                {
                    var cat = i.Category.ToString();
                    t.Format(i.Timestamp, i.LogLevel,
                        string.IsNullOrEmpty(cat) ? "" : $" [{cat}]");
                }));
    }
}

