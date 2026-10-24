namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class NoOpTelemetryTests
{
    [TestMethod]
    public void NoOp_methods_do_not_throw()
    {
        var t = new NoOpTelemetry();
        using (var operation = t.StartOperation(TelemetryKeys.Operation.Execution, "Python"))
        {
            operation.Complete();
            operation.Cancel();
            operation.Fail(new Exception("x"));
        }

        t.ReportError(new Exception("x"), TelemetryKeys.Operation.Visualization, "Mesh");
        t.Dispose();
    }
}
