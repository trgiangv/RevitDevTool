using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
namespace RevitDevTool.Test;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchDebugLogCmd : ExternalCommand
{
    public override void Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        stopwatch.Start();
        for (var stepNumber = 1; stepNumber <= 10000; stepNumber++)
        {
            Debug.WriteLine($"Processing batch item Step {stepNumber}");
            Debug.WriteLine($"Batch processing metrics for Step {stepNumber}");
            Debug.WriteLine($"Completed processing Step {stepNumber:000}");
            Debug.WriteLine($"Performance warning for Step {stepNumber:000}");
            Debug.WriteLine($"Failed to process Step {stepNumber:000}");
            Debug.WriteLine($"Critical failure in Step {stepNumber:000}");
        }
        stopwatch.Stop();
        Debug.WriteLine($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchTraceLogCmd : ExternalCommand
{
    public override void Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        stopwatch.Start();
        for (var stepNumber = 1; stepNumber <= 10000; stepNumber++)
        {
            Trace.WriteLine($"Processing batch item Step {stepNumber}");
            Trace.WriteLine($"Batch processing metrics for Step {stepNumber}");
            Trace.WriteLine($"Completed processing Step {stepNumber:000}");
            Trace.WriteLine($"Performance warning for Step {stepNumber:000}");
            Trace.WriteLine($"Failed to process Step {stepNumber:000}");
            Trace.WriteLine($"Critical failure in Step {stepNumber:000}");
        }
        stopwatch.Stop();
        Trace.WriteLine($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchConsoleLogCmd : ExternalCommand
{
    public override void Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        stopwatch.Start();
        for (var stepNumber = 1; stepNumber <= 10000; stepNumber++)
        {
            Console.WriteLine($"Processing batch item Step {stepNumber}");
            Console.WriteLine($"Batch processing metrics for Step {stepNumber}");
            Console.WriteLine($"Completed processing Step {stepNumber:000}");
            Console.WriteLine($"Performance warning for Step {stepNumber:000}");
            Console.WriteLine($"Failed to process Step {stepNumber:000}");
            Console.WriteLine($"Critical failure in Step {stepNumber:000}");
        }
        stopwatch.Stop();
        Console.WriteLine($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TraceColoredCmd : ExternalCommand
{
    public override void Execute()
    {
        Trace.TraceInformation("This is an informational message.");
        Trace.TraceWarning("This is a warning message.");
        Trace.TraceError("This is an error message.");
    }
}