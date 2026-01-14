using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using System.Diagnostics;
namespace RevitDevTool.Test;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchDebugLogCmd : ExternalCommand
{
    public override void Execute()
    {
        Task.Run(() =>
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
            Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
        });
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchTraceLogCmd : ExternalCommand
{
    public override void Execute()
    {
        Task.Run(() =>
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
            Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
        });
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchConsoleLogCmd : ExternalCommand
{
    public override void Execute()
    {
        Task.Run(() =>
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
            Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
        });
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchTraceColoredCmd : ExternalCommand
{
    public override void Execute()
    {
        Trace.TraceInformation("This is an informational message.");
        Trace.TraceWarning("This is a warning message.");
        Trace.TraceError("This is an error message.");
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchDebugLargeStringCmd : ExternalCommand
{
    public override void Execute()
    {
        Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            var largeString = new string('X', 1000);
            for (var i = 0; i < 1000; i++)
            {
                Debug.WriteLine(largeString);
            }
            stopwatch.Stop();
            Trace.TraceWarning($"Total time for large string logging: {stopwatch.ElapsedMilliseconds} ms");
        });
    }
}