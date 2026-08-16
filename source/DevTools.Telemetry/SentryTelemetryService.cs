using DevTools.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Telemetry;

/// <summary>
/// Sentry-backed telemetry: critical exceptions, coarse usage breadcrumbs on errors, and one Info event per process
/// with aggregated usage when <see cref="Flush"/> runs (host shutdown).
/// </summary>
public sealed class SentryTelemetryService : ITelemetry
{
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(2);
    private const int MaxBreakdownEntries = 50;

    private readonly IDisposable _sdkHandle;
    private readonly object _usageLock = new();
    private readonly string _hostName;
    private bool _disposed;

    private int _executionTotal;
    private int _executionSucceeded;
    private int _mcpTotal;
    private int _geometryTotal;
    private readonly Dictionary<string, int> _providerTotal = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _providerSucceeded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _mcpByProvider = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _geometryByType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _loggerTraceByLevel = new(StringComparer.OrdinalIgnoreCase);

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
            o.DefaultTags[TelemetryKeys.Tag.InstallationId] = installationId;
            o.DefaultTags[TelemetryKeys.Tag.HostApp] = hostName;
            o.DefaultTags[TelemetryKeys.Tag.HostVersion] = hostApp.VersionNumber;
            if (!string.IsNullOrWhiteSpace(hostApp.VersionBuild))
            {
                o.DefaultTags[TelemetryKeys.Tag.HostBuild] = hostApp.VersionBuild!;
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
            category: TelemetryKeys.Breadcrumb.Execution,
            level: BreadcrumbLevel.Info);
    }

    public void RecordMcpInvocation(string category)
    {
        var c = string.IsNullOrWhiteSpace(category) ? "unknown" : category.Trim();
        lock (_usageLock)
        {
            _mcpTotal++;
            _mcpByProvider.TryGetValue(c, out var current);
            _mcpByProvider[c] = current + 1;
        }

        SentrySdk.AddBreadcrumb($"category={c}", category: TelemetryKeys.Breadcrumb.Mcp, level: BreadcrumbLevel.Info);
    }

    public void RecordLoggerGeometry(string category)
    {
        var c = string.IsNullOrWhiteSpace(category) ? "unknown" : category.Trim();
        lock (_usageLock)
        {
            _geometryTotal++;
            _geometryByType.TryGetValue(c, out var current);
            _geometryByType[c] = current + 1;
        }

        SentrySdk.AddBreadcrumb($"category={c}", category: TelemetryKeys.Breadcrumb.Geometry, level: BreadcrumbLevel.Info);
    }

    public void RecordLoggerTrace(LogLevel level)
    {
        var levelKey = level.ToString();
        lock (_usageLock)
        {
            _loggerTraceByLevel.TryGetValue(levelKey, out var current);
            _loggerTraceByLevel[levelKey] = current + 1;
        }
    }

    public void RecordCriticalException(
        Exception exception,
        string feature,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        SentrySdk.ConfigureScope(
            static (scope, s) =>
            {
                scope.SetTag(TelemetryKeys.Tag.Feat, s.Feature);
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
        var snap = CaptureSnapshot();
        var logTotal = snap.LoggerTraceByLevel.Sum(x => x.Count);

        if (snap is { ExecutionTotal: 0, McpTotal: 0, GeometryTotal: 0 } && logTotal == 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _sessionUsageSent, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var evt = new SentryEvent
            {
                Level = SentryLevel.Info,
                Message = TelemetryKeys.Event.SessionUsage,
            };
            evt.SetTag(TelemetryKeys.Tag.Kind, TelemetryKeys.Fingerprint.SessionUsage);

            SetExecutionExtra(evt, snap);
            SetMcpExtra(evt, snap);
            SetGeometryExtra(evt, snap);
            SetLoggingExtra(evt, snap);

            evt.SetFingerprint(TelemetryKeys.Fingerprint.DevTool, TelemetryKeys.Fingerprint.SessionUsage, _hostName);
            SentrySdk.CaptureEvent(evt);
        }
        catch
        {
            Interlocked.Exchange(ref _sessionUsageSent, 0);
        }
    }

    private UsageSnapshot CaptureSnapshot()
    {
        lock (_usageLock)
        {
            var providers = _providerTotal
                .Select(kv => (Kind: kv.Key, Total: kv.Value, Succeeded: _providerSucceeded.GetValueOrDefault(kv.Key, 0)))
                .OrderByDescending(x => x.Total)
                .Take(MaxBreakdownEntries)
                .ToList();

            var mcp = _mcpByProvider
                .Select(kv => (Provider: kv.Key, Count: kv.Value))
                .OrderByDescending(x => x.Count)
                .Take(MaxBreakdownEntries)
                .ToList();

            var geom = _geometryByType
                .Select(kv => (Type: kv.Key, Count: kv.Value))
                .OrderByDescending(x => x.Count)
                .Take(MaxBreakdownEntries)
                .ToList();

            var log = _loggerTraceByLevel
                .Select(kv => (Level: kv.Key, Count: kv.Value))
                .OrderByDescending(x => x.Count)
                .Take(MaxBreakdownEntries)
                .ToList();

            return new UsageSnapshot(
                _executionTotal,
                _executionSucceeded,
                _mcpTotal,
                _geometryTotal,
                providers,
                mcp,
                geom,
                log);
        }
    }

    private static void SetExecutionExtra(SentryEvent evt, UsageSnapshot snap)
    {
        var failed = Math.Max(0, snap.ExecutionTotal - snap.ExecutionSucceeded);
        var providers = new Dictionary<string, object>(snap.Providers.Count);
        foreach (var (kind, total, succeeded) in snap.Providers)
        {
            providers[SanitizeExtraKey(kind)] = new Dictionary<string, int>
            {
                [TelemetryKeys.Extra.Total] = total,
                [TelemetryKeys.Extra.Succeeded] = succeeded,
                [TelemetryKeys.Extra.Failed] = Math.Max(0, total - succeeded),
            };
        }

        evt.SetExtra(TelemetryKeys.Extra.Execution, new Dictionary<string, object>
        {
            [TelemetryKeys.Extra.Total] = snap.ExecutionTotal,
            [TelemetryKeys.Extra.Succeeded] = snap.ExecutionSucceeded,
            [TelemetryKeys.Extra.Failed] = failed,
            [TelemetryKeys.Extra.Providers] = providers,
        });
    }

    private static void SetMcpExtra(SentryEvent evt, UsageSnapshot snap)
    {
        var providers = new Dictionary<string, object>(snap.McpProviders.Count);
        foreach (var (provider, count) in snap.McpProviders)
        {
            providers[SanitizeExtraKey(provider)] = count;
        }

        evt.SetExtra(TelemetryKeys.Extra.Mcp, new Dictionary<string, object>
        {
            [TelemetryKeys.Extra.Total] = snap.McpTotal,
            [TelemetryKeys.Extra.Providers] = providers,
        });
    }

    private static void SetGeometryExtra(SentryEvent evt, UsageSnapshot snap)
    {
        var types = new Dictionary<string, object>(snap.GeometryTypes.Count);
        foreach (var (type, count) in snap.GeometryTypes)
        {
            types[SanitizeExtraKey(type)] = count;
        }

        evt.SetExtra(TelemetryKeys.Extra.Geometry, new Dictionary<string, object>
        {
            [TelemetryKeys.Extra.Total] = snap.GeometryTotal,
            [TelemetryKeys.Extra.Types] = types,
        });
    }

    private static void SetLoggingExtra(SentryEvent evt, UsageSnapshot snap)
    {
        var logTotal = snap.LoggerTraceByLevel.Sum(x => x.Count);
        var levels = new Dictionary<string, object>(snap.LoggerTraceByLevel.Count);
        foreach (var (level, count) in snap.LoggerTraceByLevel)
        {
            levels[SanitizeExtraKey(level)] = count;
        }

        evt.SetExtra(TelemetryKeys.Extra.Logging, new Dictionary<string, object>
        {
            [TelemetryKeys.Extra.Total] = logTotal,
            [TelemetryKeys.Extra.Levels] = levels,
        });
    }

    private static string SanitizeExtraKey(string kind)
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

        return len == 0 ? "unknown" : new string(buf[..len].ToArray());
    }

    private readonly record struct UsageSnapshot(
        int ExecutionTotal,
        int ExecutionSucceeded,
        int McpTotal,
        int GeometryTotal,
        List<(string Kind, int Total, int Succeeded)> Providers,
        List<(string Provider, int Count)> McpProviders,
        List<(string Type, int Count)> GeometryTypes,
        List<(string Level, int Count)> LoggerTraceByLevel);
}
