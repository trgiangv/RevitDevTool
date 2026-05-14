using DevTools.Logging;

namespace DevTools.Telemetry;

/// <summary>
/// Sentry-backed telemetry: critical exceptions, coarse usage breadcrumbs on errors, and one Info event per process
/// with aggregated usage when <see cref="Flush"/> runs (host shutdown).
/// </summary>
public sealed class SentryTelemetryService : ITelemetry
{
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(2);
    private const int MaxProviderBreakdownExtras = 50;

    private readonly IDisposable _sdkHandle;
    private readonly object _usageLock = new();
    private readonly string _hostName;
    private bool _disposed;

    private int _executionTotal;
    private int _executionSucceeded;
    private int _mcpTotal;
    private int _geometryTotal;
    private long _loggerTraceRawLines;
    private readonly Dictionary<string, int> _providerTotal = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _providerSucceeded = new(StringComparer.OrdinalIgnoreCase);

    private int _sessionUsageSent;

    public SentryTelemetryService(string dsn, IHostAppInfo hostApp)
    {
        if (hostApp is null)
        {
            throw new ArgumentNullException(nameof(hostApp));
        }

        if (string.IsNullOrWhiteSpace(dsn))
        {
            throw new ArgumentException(@"DSN is required.", nameof(dsn));
        }

        dsn = dsn.Trim();
        var version = typeof(SentryTelemetryService).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        var installationId = InstallationId.GetOrCreate();
        var hostName = hostApp.Host.ToString();
        _hostName = hostName;
        var release = $"devtool-{hostName}@{version}";

        _sdkHandle = SentrySdk.Init(o =>
        {
            o.Dsn = dsn;
            o.Release = release;
            o.DefaultTags["installation_id"] = installationId;
            o.DefaultTags["host_app"] = hostName;
            o.DefaultTags["host_version"] = hostApp.VersionNumber;
            if (!string.IsNullOrWhiteSpace(hostApp.VersionBuild))
            {
                o.DefaultTags["host_build"] = hostApp.VersionBuild!;
            }

            o.TracesSampleRate = 0;
            o.AutoSessionTracking = false;
            o.DisableAppDomainProcessExitFlush();
        });
    }

    public void RecordExecutionInvocation(string providerKind, bool succeeded)
    {
        var kind = string.IsNullOrWhiteSpace(providerKind) ? "unknown" : providerKind.Trim();
        lock (_usageLock)
        {
            _executionTotal++;
            if (succeeded)
            {
                _executionSucceeded++;
            }

            _providerTotal.TryGetValue(kind, out var pt);
            _providerTotal[kind] = pt + 1;
            if (succeeded)
            {
                _providerSucceeded.TryGetValue(kind, out var ps);
                _providerSucceeded[kind] = ps + 1;
            }
        }

        SentrySdk.AddBreadcrumb(
            $"provider={kind} success={succeeded}",
            category: "telemetry.execution",
            level: BreadcrumbLevel.Info);
    }

    public void RecordMcpInvocation(string category)
    {
        var c = string.IsNullOrWhiteSpace(category) ? "unknown" : category.Trim();
        lock (_usageLock)
        {
            _mcpTotal++;
        }

        SentrySdk.AddBreadcrumb($"category={c}", category: "telemetry.mcp", level: BreadcrumbLevel.Info);
    }

    public void RecordLoggerGeometry(string category)
    {
        var c = string.IsNullOrWhiteSpace(category) ? "unknown" : category.Trim();
        lock (_usageLock)
        {
            _geometryTotal++;
        }

        SentrySdk.AddBreadcrumb($"category={c}", category: "telemetry.geometry", level: BreadcrumbLevel.Info);
    }

    public void RecordLoggerTrace()
    {
        Interlocked.Increment(ref _loggerTraceRawLines);
    }

    public void RecordCriticalException(
        Exception exception,
        string feature,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        SentrySdk.ConfigureScope(
            static (scope, s) =>
            {
                scope.SetTag("telemetry.feature", s.Feature);
                if (s.Tags is null)
                {
                    return;
                }

                foreach (var kv in s.Tags)
                {
                    scope.SetTag(kv.Key, TelemetryPathScrubber.Scrub(kv.Value));
                }
            },
            (Feature: feature, Tags: tags));

        SentrySdk.CaptureException(exception);
    }

    public void Flush()
    {
        TrySendSessionUsageSummary();
        SentrySdk.Flush(FlushTimeout);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Flush();
        _sdkHandle.Dispose();
    }

    private void TrySendSessionUsageSummary()
    {
        int execTotal;
        int execSucceeded;
        int mcpTotal;
        int geomTotal;
        var loggerTraceRaw = Interlocked.Read(ref _loggerTraceRawLines);
        List<(string Kind, int Total, int Succeeded)> providers;
        lock (_usageLock)
        {
            execTotal = _executionTotal;
            execSucceeded = _executionSucceeded;
            mcpTotal = _mcpTotal;
            geomTotal = _geometryTotal;
            providers = new List<(string, int, int)>(_providerTotal.Count);
            foreach (var kv in _providerTotal)
            {
                _providerSucceeded.TryGetValue(kv.Key, out var ok);
                providers.Add((kv.Key, kv.Value, ok));
            }
        }

        if (execTotal == 0 && mcpTotal == 0 && geomTotal == 0 && loggerTraceRaw == 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _sessionUsageSent, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var loggerTraceNet = Math.Max(0L, loggerTraceRaw - geomTotal);

            providers.Sort(static (a, b) => b.Total.CompareTo(a.Total));

            var evt = new SentryEvent
            {
                Level = SentryLevel.Info,
                Message = "devtool.session.usage",
            };
            evt.SetTag("telemetry.kind", "session_usage");
            evt.SetExtra("execution_total", execTotal);
            evt.SetExtra("execution_succeeded", execSucceeded);
            evt.SetExtra("execution_failed", Math.Max(0, execTotal - execSucceeded));
            evt.SetExtra("mcp_invocations", mcpTotal);
            evt.SetExtra("geometry_events", geomTotal);
            evt.SetExtra("logger_trace_lines", loggerTraceNet);

            var n = 0;
            foreach (var (kind, total, succeeded) in providers)
            {
                if (n >= MaxProviderBreakdownExtras)
                {
                    break;
                }

                var key = SanitizeProviderKeyForExtra(kind);
                evt.SetExtra($"provider.{key}", $"{succeeded}/{total}");
                n++;
            }

            evt.SetFingerprint("devtool", "session-usage", _hostName);
            SentrySdk.CaptureEvent(evt);
        }
        catch
        {
            Interlocked.Exchange(ref _sessionUsageSent, 0);
        }
    }

    /// <summary>Safe segment for Sentry extra keys (alphanumeric and hyphen).</summary>
    private static string SanitizeProviderKeyForExtra(string kind)
    {
        Span<char> buf = stackalloc char[40];
        var len = 0;
        foreach (var ch in kind)
        {
            if (len >= buf.Length)
            {
                break;
            }

            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                buf[len++] = ch;
            }
        }

        return len == 0 ? "unknown" : new string(buf.Slice(0, len).ToArray());
    }
}
