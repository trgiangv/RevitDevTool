namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class TelemetryReporterTests
{
    [TestMethod]
    public void ShouldReport_returns_true_for_thrown_internal_exception()
    {
        Assert.IsTrue(TelemetryReporter.ShouldReport(Throw(new InvalidOperationException("x"))));
    }

    [TestMethod]
    public void ShouldReport_returns_false_for_cancellation_and_timeout()
    {
        Assert.IsFalse(TelemetryReporter.ShouldReport(Throw(new OperationCanceledException())));
        Assert.IsFalse(TelemetryReporter.ShouldReport(Throw(new TaskCanceledException())));
        Assert.IsFalse(TelemetryReporter.ShouldReport(Throw(new TimeoutException())));
        Assert.IsFalse(TelemetryReporter.ShouldReport(
            Throw(new InvalidOperationException("wrap", new TimeoutException()))));
    }

    [TestMethod]
    public void ShouldReport_returns_false_for_external_exception()
    {
        var ex = TelemetryReporter.MarkExternal(Throw(new InvalidOperationException("script")));
        Assert.IsTrue(TelemetryReporter.IsExternal(ex));
        Assert.IsFalse(TelemetryReporter.ShouldReport(ex));
    }

    [TestMethod]
    public void IsExternal_follows_inner_exceptions()
    {
        var inner = TelemetryReporter.MarkExternal(new InvalidOperationException("script"));
        Assert.IsTrue(TelemetryReporter.IsExternal(new Exception("wrap", inner)));
    }

    [TestMethod]
    public void IsInternal_returns_false_without_devtools_frames()
    {
        Assert.IsFalse(TelemetryReporter.IsInternal(new InvalidOperationException("never thrown")));
        Assert.IsFalse(TelemetryReporter.ShouldReport(new InvalidOperationException("never thrown")));
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
}
