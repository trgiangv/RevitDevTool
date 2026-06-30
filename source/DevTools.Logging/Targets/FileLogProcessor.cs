using System.IO;
using DevTools.Logging.Abstractions;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;
namespace DevTools.Logging.Targets;

/// <summary>
/// Manages file logging by dynamically adding/removing a <see cref="ZLoggerRollingFileLoggerProvider"/>
/// to the <see cref="ILoggerFactory"/> at runtime.
/// </summary>
public sealed class FileLogProcessor(ILoggerFactory loggerFactory, IHostAppInfo appInfo) : IFileLogTarget
{
    private ZLoggerRollingFileLoggerProvider? _provider;
    private bool _disposed;

    public void Enable<T>(T options)
    {
        if (options is not FileLoggingOptions fileOptions)
            throw new ArgumentException($"Expected {nameof(FileLoggingOptions)}, got {typeof(T).Name}");

        var folder = fileOptions.LogFolder;
        Directory.CreateDirectory(folder);

        var ext = fileOptions.Format == SaveFormat.Json ? "json" : "log";
        var app = appInfo.Host;
        var ver = appInfo.VersionBuild;
        var pid = appInfo.ProcessId;

        var rollingOptions = new ZLoggerRollingFileOptions
        {
            IncludeScopes = true,
            FilePathSelector = (dt, seq) =>
                Path.Combine(folder, $"log_{app}_{ver}_{pid}_{dt:yyyyMMddTHHmmss}_{seq:D3}.{ext}"),
            RollingInterval = fileOptions.RollingInterval,
            FileShared = true
        };

        ConfigureFormatter(rollingOptions, fileOptions.Format);

        var newProvider = new ZLoggerRollingFileLoggerProvider(rollingOptions);
        loggerFactory.AddProvider(newProvider);

        var old = Interlocked.Exchange(ref _provider, newProvider);
        old?.Dispose();
    }

    public void Disable()
    {
        var old = Interlocked.Exchange(ref _provider, null);
        old?.Dispose();
    }

    void IDisposable.Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disable();
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
                    $"[{0:local-timeonly} {1:short}]{2} ",
                    (in t, in i) =>
                    {
                        var cat = i.Category.ToString();
                        t.Format(i.Timestamp, i.LogLevel,
                            string.IsNullOrEmpty(cat) ? "" : $" [{cat}]");
                    }));
        }
    }
}
