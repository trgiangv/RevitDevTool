using DevTools.Hosting;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class SentryTelemetryTests
{
    private const string DummyDsn = "https://publickey@127.0.0.1/1";

    [TestMethod]
    public void Constructor_validates_arguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SentryTelemetry(null!, DummyDsn));
        Assert.ThrowsExactly<ArgumentException>(() => new SentryTelemetry(new FakeHostAppInfo(), "  "));
    }

    [TestMethod]
    public void Operations_and_errors_do_not_throw()
    {
        using var telemetry = new SentryTelemetry(new FakeHostAppInfo { VersionBuild = "26.0" }, DummyDsn);

        telemetry.StartOperation(TelemetryKeys.Operation.Execution, "  ").Complete();
        telemetry.StartOperation(TelemetryKeys.Operation.Execution, "CSharp").Fail();
        telemetry.StartOperation(TelemetryKeys.Operation.Execution, "Python").Fail(new OperationCanceledException());
        telemetry.StartOperation(TelemetryKeys.Operation.Mcp, "BuiltIn").Fail(new InvalidOperationException(@"C:\secret\file.py"));
        using (var operation = telemetry.StartOperation(TelemetryKeys.Operation.Mcp, "Python"))
        {
            operation.Cancel();
            operation.Complete();
        }

        var renderError = new InvalidOperationException("render");
        telemetry.ReportError(renderError, TelemetryKeys.Operation.Visualization, "MeshVisualizationServer");
        telemetry.ReportError(renderError, TelemetryKeys.Operation.Visualization, "MeshVisualizationServer");
        telemetry.Dispose();
    }

    internal sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host { get; init; } = HostApp.Revit;
        public string VersionNumber { get; init; } = "2025";
        public string? VersionBuild { get; init; }
        public int ProcessId { get; init; } = 42;
    }
}
