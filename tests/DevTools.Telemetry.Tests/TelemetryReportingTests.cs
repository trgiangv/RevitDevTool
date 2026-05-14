namespace DevTools.Telemetry.Tests;

public sealed class TelemetryReportingTests
{
    [Fact]
    public void ShouldReport_returns_false_for_operation_canceled()
    {
        Assert.False(TelemetryReporting.ShouldReportCriticalException(new OperationCanceledException()));
    }

    [Fact]
    public void ShouldReport_returns_false_for_timeout()
    {
        Assert.False(TelemetryReporting.ShouldReportCriticalException(new TimeoutException()));
    }

    [Fact]
    public void ShouldReport_returns_true_for_invalid_operation()
    {
        Assert.True(TelemetryReporting.ShouldReportCriticalException(new InvalidOperationException("x")));
    }
}
