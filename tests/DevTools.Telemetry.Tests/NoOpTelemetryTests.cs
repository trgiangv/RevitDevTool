namespace DevTools.Telemetry.Tests;

public sealed class NoOpTelemetryTests
{
    [Fact]
    public void NoOp_methods_do_not_throw()
    {
        var t = new NoOpTelemetry();
        t.RecordExecutionInvocation("Assembly", true);
        t.RecordMcpInvocation("Assembly");
        t.RecordLoggerGeometry("mesh");
        t.RecordLoggerTrace();
        t.RecordCriticalException(new Exception("x"), "test", null);
        t.Flush();
        t.Dispose();
    }
}
