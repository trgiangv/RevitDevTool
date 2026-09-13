namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class TelemetryReportingTests
{
    [TestMethod]
    public void ShouldReport_returns_false_for_operation_canceled()
    {
        Assert.IsFalse(TelemetryReporting.ShouldReportCriticalException(new OperationCanceledException()));
    }

    [TestMethod]
    public void ShouldReport_returns_false_for_timeout()
    {
        Assert.IsFalse(TelemetryReporting.ShouldReportCriticalException(new TimeoutException()));
    }

    [TestMethod]
    public void ShouldReport_returns_true_for_invalid_operation()
    {
        Assert.IsTrue(TelemetryReporting.ShouldReportCriticalException(new InvalidOperationException("x")));
    }
}
