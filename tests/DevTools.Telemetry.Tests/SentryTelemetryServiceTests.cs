using System.Diagnostics;
using DevTools.Hosting;
using DevTools.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class SentryTelemetryServiceTests
{
    private const string DummyDsn = "https://publickey@127.0.0.1/1";

    [TestMethod]
    public void Constructor_validates_arguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SentryTelemetryService(DummyDsn, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new SentryTelemetryService("  ", new FakeHostAppInfo()));
    }

    [TestMethod]
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

    [TestMethod]
    public void Flush_with_no_usage_is_noop()
    {
        using var telemetry = new SentryTelemetryService(DummyDsn, new FakeHostAppInfo());
        telemetry.Flush();
    }

    [TestMethod]
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

[TestClass]
public sealed class TelemetryExtensionsTests
{
    [TestMethod]
    public void AddDevToolsTelemetry_registers_ITelemetry()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddSingleton<IHostAppInfo>(new SentryTelemetryServiceTests.FakeHostAppInfo());
        builder.AddDevToolsTelemetry(_ => false, _ => null);

        using var host = builder.Build();
        Assert.IsInstanceOfType<NoOpTelemetry>(host.Services.GetRequiredService<ITelemetry>());
    }
}

[TestClass]
public sealed class TelemetryReportingCoverageTests
{
    [TestMethod]
    public void ShouldReport_returns_false_for_task_canceled_and_inner_timeout()
    {
        Assert.IsFalse(TelemetryReporting.ShouldReportCriticalException(new TaskCanceledException()));
        Assert.IsFalse(TelemetryReporting.ShouldReportCriticalException(
            new InvalidOperationException("wrap", new TimeoutException())));
    }

    [TestMethod]
    public void ShouldReport_returns_true_for_other_exceptions()
    {
        Assert.IsTrue(TelemetryReporting.ShouldReportCriticalException(new Exception("x")));
    }
}
