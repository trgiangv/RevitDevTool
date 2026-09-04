using System.Diagnostics;
using DevTools.Hosting;
using DevTools.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Telemetry.Tests;

public sealed class SentryTelemetryServiceTests
{
    private const string DummyDsn = "https://publickey@127.0.0.1/1";

    [Fact]
    public void Constructor_validates_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new SentryTelemetryService(DummyDsn, null!));
        Assert.Throws<ArgumentException>(() => new SentryTelemetryService("  ", new FakeHostAppInfo()));
    }

    [Fact]
    public void Record_methods_and_flush_do_not_throw()
    {
        var prev = Environment.GetEnvironmentVariable("SENTRY_DSN");
        try
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", null);
            using var telemetry = new SentryTelemetryService(DummyDsn, new FakeHostAppInfo
            {
                VersionBuild = "26.0",
            });

            telemetry.RecordExecutionInvocation("  ", true);
            telemetry.RecordExecutionInvocation("csharp", false);
            telemetry.RecordMcpInvocation(string.Empty);
            telemetry.RecordMcpInvocation("tools");
            telemetry.RecordLoggerGeometry("mesh");
            telemetry.RecordLoggerTrace(LogLevel.Warning);
            telemetry.RecordCriticalException(
                new InvalidOperationException("boom"),
                "feature",
                new Dictionary<string, string> { ["path"] = @"C:\secret\file.py" });

            telemetry.Flush();
            telemetry.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", prev);
        }
    }

    [Fact]
    public void Flush_with_no_usage_is_noop()
    {
        using var telemetry = new SentryTelemetryService(DummyDsn, new FakeHostAppInfo());
        telemetry.Flush();
    }

    [Fact]
    public void BuiltInSentryDsn_is_https_endpoint()
    {
        Assert.StartsWith("https://", BuiltInSentryDsn.Value, StringComparison.Ordinal);
    }

    internal sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host { get; init; } = HostApp.Revit;
        public string VersionNumber { get; init; } = "2025";
        public string? VersionBuild { get; init; }
        public int ProcessId { get; init; } = 42;
    }
}

public sealed class TelemetryExtensionsTests
{
    [Fact]
    public void AddDevToolsTelemetry_registers_ITelemetry()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddSingleton<IHostAppInfo>(new SentryTelemetryServiceTests.FakeHostAppInfo());
        builder.AddDevToolsTelemetry(_ => false, _ => null);

        using var host = builder.Build();
        Assert.IsType<NoOpTelemetry>(host.Services.GetRequiredService<ITelemetry>());
    }
}

public sealed class TelemetryReportingCoverageTests
{
    [Fact]
    public void ShouldReport_returns_false_for_task_canceled_and_inner_timeout()
    {
        Assert.False(TelemetryReporting.ShouldReportCriticalException(new TaskCanceledException()));
        Assert.False(TelemetryReporting.ShouldReportCriticalException(
            new InvalidOperationException("wrap", new TimeoutException())));
    }

    [Fact]
    public void ShouldReport_returns_true_for_other_exceptions()
    {
        Assert.True(TelemetryReporting.ShouldReportCriticalException(new Exception("x")));
    }
}
