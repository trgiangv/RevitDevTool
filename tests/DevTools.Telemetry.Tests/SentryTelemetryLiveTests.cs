using DevTools.Hosting;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class SentryTelemetryLiveTests
{
    private const string LiveSwitch = "DEVTOOLS_TELEMETRY_LIVE";
    private const string TestEnvironment = "test";
    private const string TestUser = "tester";

    [TestMethod]
    [TestCategory("Live")]
    public async Task Live_sends_operations_and_internal_errors_to_test_environment()
    {
        if (Environment.GetEnvironmentVariable(LiveSwitch) != "1")
            Assert.Inconclusive($"Set {LiveSwitch}=1 to send to Sentry (environment={TestEnvironment}, user={TestUser}).");

        using (var telemetry = new SentryTelemetry(
                   new LiveHostAppInfo(),
                   SentryTelemetry.Dsn,
                   TestEnvironment,
                   TestUser))
        {
            using (var operation = telemetry.StartOperation(TelemetryKeys.Operation.Execution, "Python"))
            {
                await Task.Delay(120);
                operation.Complete();
            }

            telemetry.StartOperation(TelemetryKeys.Operation.Execution, "CSharp").Fail();
            telemetry.StartOperation(TelemetryKeys.Operation.Execution, "IronPython")
                .Fail(TelemetryReporter.MarkExternal(Throw(new InvalidOperationException("user script error"))));
            telemetry.StartOperation(TelemetryKeys.Operation.Test, "NUnit").Cancel();
            telemetry.StartOperation(TelemetryKeys.Operation.Mcp, "BuiltIn")
                .Fail(Throw(new InvalidOperationException(@"Live internal error reading C:\Users\tester\Projects\Tower.rvt")));

            var renderError = Throw(new ArgumentOutOfRangeException("buffer", "Live visualization error"));
            telemetry.ReportError(renderError, TelemetryKeys.Operation.Visualization, "MeshVisualizationServer");
            telemetry.ReportError(renderError, TelemetryKeys.Operation.Visualization, "MeshVisualizationServer");
        }
    }

    private static Exception Throw(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private sealed class LiveHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => "25.4.0";
        public int ProcessId => Environment.ProcessId;
    }
}
