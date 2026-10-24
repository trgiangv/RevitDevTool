using DevTools.Hosting;
using System.Collections.Concurrent;

namespace DevTools.Telemetry;

/// <summary>
/// One Sentry transaction per operation, release-health sessions per host process,
/// and error reports only for internal (DevTools) failures.
/// </summary>
public sealed class SentryTelemetry : ITelemetry
{
    private const string Dsn = "https://2e64ea9f4ccffe2bbb4d6e8757515962@o4510710170845184.ingest.de.sentry.io/4511389304815696";

    private readonly IDisposable _sdkHandle;
    private readonly ConcurrentDictionary<string, byte> _reportedErrors = new();
    private bool _disposed;

    public SentryTelemetry(IHostAppInfo hostApp) : this(hostApp, Dsn)
    {
    }

    internal SentryTelemetry(IHostAppInfo hostApp, string dsn)
    {
        ArgumentNullException.ThrowIfNull(hostApp);
        ArgumentException.ThrowIfNullOrWhiteSpace(dsn);

        var version = typeof(SentryTelemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        var userId = UserIdentity.Get();
        var hostName = hostApp.Host.ToString();
        var release = $"devtool-{hostName}@{version}";

        _sdkHandle = SentrySdk.Init(o =>
        {
            o.Dsn = dsn;
            o.Release = release;
            o.IsGlobalModeEnabled = true;
            o.SendDefaultPii = false;
            o.DefaultTags[TelemetryKeys.Tag.HostApp] = hostName;
            o.DefaultTags[TelemetryKeys.Tag.HostVersion] = hostApp.VersionNumber;
            if (!string.IsNullOrWhiteSpace(hostApp.VersionBuild))
            {
                o.DefaultTags[TelemetryKeys.Tag.HostBuild] = hostApp.VersionBuild!;
            }

            o.AutoSessionTracking = true;
            o.TracesSampleRate = 1.0;
            o.SetBeforeSend(static evt => evt.Exception is { } ex && !TelemetryReporter.ShouldReport(ex)
                ? null
                : SentryEventScrubber.Scrub(evt));

            o.DisableUnobservedTaskExceptionCapture();
            o.DisableAppDomainProcessExitFlush();
#if NET
            o.DisableDiagnosticSourceIntegration();
            o.DisableSystemDiagnosticsMetricsIntegration();
#endif
        });

        SentrySdk.ConfigureScope(
            static (scope, id) => scope.User = new SentryUser { Id = id },
            userId);
    }

    public ITelemetryOperation StartOperation(string operation, string kind)
    {
        var k = string.IsNullOrWhiteSpace(kind) ? "unknown" : kind.Trim();
        var transaction = SentrySdk.StartTransaction($"{operation}.{k}", operation);
        transaction.SetTag(TelemetryKeys.Tag.Provider, k);
        return new Operation(transaction, operation, k);
    }

    public void ReportError(Exception exception, string operation, string kind)
    {
        if (!TelemetryReporter.ShouldReport(exception)
            || !_reportedErrors.TryAdd($"{operation}|{kind}|{exception.GetType().FullName}", 0))
        {
            return;
        }

        Report(exception, operation, kind);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sdkHandle.Dispose();
    }

    private static void Report(Exception exception, string operation, string kind)
    {
        SentrySdk.CaptureException(exception, scope =>
        {
            scope.SetTag(TelemetryKeys.Tag.Operation, operation);
            scope.SetTag(TelemetryKeys.Tag.Provider, kind);
        });
    }

    private sealed class Operation(ITransactionTracer transaction, string operation, string kind) : ITelemetryOperation
    {
        private int _ended;

        public void Complete() => End(SpanStatus.Ok);

        public void Cancel() => End(SpanStatus.Cancelled);

        public void Fail(Exception? exception = null)
        {
            if (exception is not null && TelemetryReporter.IsCancellation(exception))
            {
                Cancel();
                return;
            }

            if (exception is null || !TelemetryReporter.ShouldReport(exception))
            {
                End(SpanStatus.InvalidArgument);
                return;
            }

            if (End(SpanStatus.InternalError))
            {
                Report(exception, operation, kind);
            }
        }

        public void Dispose() => Cancel();

        private bool End(SpanStatus status)
        {
            if (Interlocked.Exchange(ref _ended, 1) != 0)
            {
                return false;
            }

            transaction.Finish(status);
            SentrySdk.AddBreadcrumb(
                $"{transaction.Name} {status}",
                category: operation,
                level: status == SpanStatus.InternalError ? BreadcrumbLevel.Error : BreadcrumbLevel.Info);
            return true;
        }
    }
}
