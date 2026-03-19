using System.Collections.Concurrent;
using System.IO;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace DevTools.Logging;

[ProviderAlias("DevToolsFile")]
public sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope, IAsyncDisposable
{
    private readonly IAppInfo _appInfo;
    private readonly IContextEnricher? _enricher;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    private volatile ZLoggerRollingFileLoggerProvider? _inner;
    private IDisposable? _enricherScope;
    private IExternalScopeProvider? _scopeProvider;
    private bool _disposed;

    public FileLoggerProvider(IAppInfo appInfo, IContextEnricher? enricher = null)
    {
        _appInfo = appInfo;
        _enricher = enricher;
    }

    public bool IsActive => _inner != null;

    public void Restart(FileLoggingOptions options)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            DisposeInner();

            if (!options.Enabled) return;

            var folder = options.LogFolder;
            Directory.CreateDirectory(folder);

            var ext = options.Format == SaveFormat.Json ? "json" : "log";
            var app = _appInfo.AppName;
            var ver = _appInfo.VersionBuild;
            var pid = _appInfo.ProcessId;

            var rollingOptions = new ZLoggerRollingFileOptions
            {
                IncludeScopes = true,
                FilePathSelector = (dt, seq) =>
                    Path.Combine(folder, $"log_{app}_{ver}_{pid}_{dt:yyyyMMddTHHmmss}_{seq:D3}.{ext}"),
                RollingInterval = options.RollingInterval
            };

            ConfigureFormatter(rollingOptions, options.Format);

            var newInner = new ZLoggerRollingFileLoggerProvider(rollingOptions);
            if (_scopeProvider != null)
                newInner.SetScopeProvider(_scopeProvider);

            _inner = newInner;

            if (_enricher == null) return;
            var staticProps = _enricher.GetStaticProperties();
            if (staticProps.Count > 0)
            {
                var scopeLogger = newInner.CreateLogger("DevTools");
                _enricherScope = scopeLogger.BeginScope(staticProps)!;
            }
        }
    }

    public void Stop()
    {
        lock (_lock) { DisposeInner(); }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, static (name, owner) => new FileLogger(owner, name), this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
        _inner?.SetScopeProvider(scopeProvider);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            DisposeInner();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        ZLoggerRollingFileLoggerProvider? inner;
        lock (_lock)
        {
            if (_disposed) return;
            _enricherScope?.Dispose();
            _enricherScope = null;
            inner = _inner;
            _inner = null;
            _disposed = true;
        }

        if (inner != null)
            await inner.DisposeAsync().ConfigureAwait(false);
    }

    internal ILogger? GetInnerLogger(string categoryName)
    {
        return _inner?.CreateLogger(categoryName);
    }

    private void ThrowIfDisposed()
    {
#if NET
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed) throw new ObjectDisposedException(nameof(FileLoggerProvider));
#endif
    }

    private static void ConfigureFormatter(ZLoggerRollingFileOptions options, SaveFormat format)
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

    private void DisposeInner()
    {
        _enricherScope?.Dispose();
        _enricherScope = null;
        var old = _inner;
        _inner = null;
        old?.Dispose();
    }

    internal sealed class FileLogger(FileLoggerProvider owner, string categoryName) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None
                && owner.GetInnerLogger(categoryName)?.IsEnabled(logLevel) == true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var inner = owner.GetInnerLogger(categoryName);
            if (inner == null) return;

            var enricher = owner._enricher;
            var dynamicProps = enricher?.GetDynamicProperties();
            if (dynamicProps != null)
            {
                using (inner.BeginScope(dynamicProps)!)
                {
                    inner.Log(logLevel, eventId, state, exception, formatter);
                }
            }
            else
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return owner.GetInnerLogger(categoryName)?.BeginScope(state);
        }
    }
}
