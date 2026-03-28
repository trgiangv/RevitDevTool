using System.IO;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace DevTools.Logging;

/// <summary>
/// Manages file logging by dynamically adding/removing a <see cref="ZLoggerRollingFileLoggerProvider"/>
/// to the <see cref="ILoggerFactory"/> at runtime. No custom <see cref="IAsyncLogProcessor"/>,
/// no reflection — uses ZLogger's rolling file provider exactly as designed.
/// MEL's built-in fan-out ensures every <c>ILogger&lt;T&gt;.Log()</c> call reaches the file provider.
/// </summary>
public sealed class FileLogProcessor(ILoggerFactory loggerFactory) : IDisposable
{
    private ZLoggerRollingFileLoggerProvider? _provider;
    private bool _disposed;

    public void Restart(FileLoggingOptions options, IAppInfo appInfo)
    {
        if (!options.Enabled)
        {
            Stop();
            return;
        }

        var folder = options.LogFolder;
        Directory.CreateDirectory(folder);

        var ext = options.Format == SaveFormat.Json ? "json" : "log";
        var app = appInfo.AppName;
        var ver = appInfo.VersionBuild;
        var pid = appInfo.ProcessId;

        var rollingOptions = new ZLoggerRollingFileOptions
        {
            IncludeScopes = true,
            FilePathSelector = (dt, seq) =>
                Path.Combine(folder, $"log_{app}_{ver}_{pid}_{dt:yyyyMMddTHHmmss}_{seq:D3}.{ext}"),
            RollingInterval = options.RollingInterval
        };

        ConfigureFormatter(rollingOptions, options.Format);

        var newProvider = new ZLoggerRollingFileLoggerProvider(rollingOptions);
        loggerFactory.AddProvider(newProvider);

        var old = Interlocked.Exchange(ref _provider, newProvider);
        old?.Dispose();
    }

    public void Stop()
    {
        var old = Interlocked.Exchange(ref _provider, null);
        old?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private static void ConfigureFormatter(ZLoggerOptions options, SaveFormat format)
    {
        if (format == SaveFormat.Json)
        {
            options.UseJsonFormatter();
        }
        else
        {
            options.UsePlainTextFormatter(formatter =>
                formatter.SetPrefixFormatter(
                    $"[{0:local-timeonly} {1:short}] ",
                    (in t, in i) => t.Format(i.Timestamp, i.LogLevel)));
        }
    }
}
