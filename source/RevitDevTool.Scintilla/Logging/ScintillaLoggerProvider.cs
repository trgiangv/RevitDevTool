using System.Collections;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Contracts;

namespace RevitDevTool.Scintilla.Logging;

internal sealed class ScintillaLoggerProvider(ILogIngress ingress) : ILoggerProvider, ISupportExternalScope
{
    private readonly ILogIngress _ingress = ingress;
    private readonly ScintillaLoggerOptions _options = new();
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public ScintillaLoggerProvider(ILogIngress ingress, ScintillaLoggerOptions options) : this(ingress)
    {
        _options = options;
    }

    public ILogger CreateLogger(string categoryName) => new ScintillaLogger(_ingress, categoryName, _options, () => _scopeProvider);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private sealed class ScintillaLogger(
        ILogIngress ingress,
        string categoryName,
        ScintillaLoggerOptions options,
        Func<IExternalScopeProvider> scopeProviderFactory) : ILogger
    {
        private readonly ILogIngress _ingress = ingress;
        private readonly string _categoryName = categoryName;
        private readonly ScintillaLoggerOptions _options = options;
        private readonly Func<IExternalScopeProvider> _scopeProviderFactory = scopeProviderFactory;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => _scopeProviderFactory().Push(state);
        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None || logLevel < _options.MinimumLevel)
                return false;

            return _options.CategoryFilter?.Invoke(_categoryName) ?? true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var properties = BuildProperties(state, eventId, _options.IncludeScopes ? _scopeProviderFactory() : null);
            _ingress.TryPost(new LogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Level = MapLevel(logLevel),
                Message = message,
                ExceptionText = exception?.ToString(),
                Source = _categoryName,
                Properties = properties
            });
        }

        private static LogSeverity MapLevel(LogLevel level) => level switch
        {
            LogLevel.Trace => LogSeverity.Trace,
            LogLevel.Debug => LogSeverity.Debug,
            LogLevel.Information => LogSeverity.Information,
            LogLevel.Warning => LogSeverity.Warning,
            LogLevel.Error => LogSeverity.Error,
            LogLevel.Critical => LogSeverity.Critical,
            _ => LogSeverity.Information
        };

        private static IReadOnlyDictionary<string, object?> BuildProperties<TState>(
            TState state,
            EventId eventId,
            IExternalScopeProvider? scopeProvider)
        {
            Dictionary<string, object?>? properties = null;
            static Dictionary<string, object?> Ensure(Dictionary<string, object?>? target)
                => target ?? new Dictionary<string, object?>(StringComparer.Ordinal);

            if (eventId != default)
            {
                properties = Ensure(properties);
                properties["EventId"] = eventId.Id;
                if (!string.IsNullOrWhiteSpace(eventId.Name))
                    properties["EventName"] = eventId.Name;
            }

            if (state is IEnumerable<KeyValuePair<string, object?>> statePairs)
            {
                foreach (var pair in statePairs)
                {
                    if (string.Equals(pair.Key, "{OriginalFormat}", StringComparison.Ordinal))
                        continue;
                    properties = Ensure(properties);
                    properties[pair.Key] = pair.Value;
                }
            }
            else if (state is not null && state is not string)
            {
                properties = Ensure(properties);
                properties["State"] = state;
            }

            if (scopeProvider is not null)
            {
                scopeProvider.ForEachScope((scope, bag) =>
                {
                    if (scope is null)
                        return;

                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopePairs)
                    {
                        foreach (var pair in scopePairs)
                        {
                            var key = $"Scope.{pair.Key}";
                            bag[key] = pair.Value;
                        }
                        return;
                    }

                    if (scope is IEnumerable enumerable and not string)
                    {
                        var index = 0;
                        foreach (var item in enumerable)
                        {
                            bag[$"Scope[{index++}]"] = item;
                        }
                        return;
                    }

                    bag[$"Scope.{scope.GetType().Name}"] = scope;
                }, properties ??= new Dictionary<string, object?>(StringComparer.Ordinal));
            }

            return properties ?? LogEntry.EmptyProperties;
        }
    }
}
