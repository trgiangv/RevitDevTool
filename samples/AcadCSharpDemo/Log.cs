using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;

namespace AcadCSharpDemo;

[PublicAPI]
public static class LogCommands
{
    [CommandMethod("BatchDebugLog")]
    public static void BatchDebugLog()
    {
        var stopwatch = Stopwatch.StartNew();
        for (var step = 1; step <= 10000; step++)
        {
            Debug.WriteLine($"Processing batch item Step {step}");
            Debug.WriteLine($"Batch processing metrics for Step {step}");
            Debug.WriteLine($"Completed processing Step {step:000}");
            Debug.WriteLine($"Performance warning for Step {step:000}");
            Debug.WriteLine($"Failed to process Step {step:000}");
            Debug.WriteLine($"Critical failure in Step {step:000}");
        }
        stopwatch.Stop();
        Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
    }

    [CommandMethod("BatchTraceLog")]
    public static void BatchTraceLog()
    {
        var stopwatch = Stopwatch.StartNew();
        for (var step = 1; step <= 10000; step++)
        {
            Trace.WriteLine($"Processing batch item Step {step}");
            Trace.WriteLine($"Batch processing metrics for Step {step}");
            Trace.WriteLine($"Completed processing Step {step:000}");
            Trace.WriteLine($"Performance warning for Step {step:000}");
            Trace.WriteLine($"Failed to process Step {step:000}");
            Trace.WriteLine($"Critical failure in Step {step:000}");
        }
        stopwatch.Stop();
        Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
    }

    [CommandMethod("BatchConsoleLog")]
    public static void BatchConsoleLog()
    {
        var stopwatch = Stopwatch.StartNew();
        for (var step = 1; step <= 10000; step++)
        {
            Console.WriteLine($"Processing batch item Step {step}");
            Console.WriteLine($"Batch processing metrics for Step {step}");
            Console.WriteLine($"Completed processing Step {step:000}");
            Console.WriteLine($"Performance warning for Step {step:000}");
            Console.WriteLine($"Failed to process Step {step:000}");
            Console.WriteLine($"Critical failure in Step {step:000}");
        }
        stopwatch.Stop();
        Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms");
    }

    [CommandMethod("SerilogFormatTest")]
    public static void SerilogFormatTest()
    {
        // Basic text
        Trace.TraceInformation("=== Serilog Formatting Test Start ===");

        // Different log levels
        Trace.TraceInformation("This is an information message");
        Trace.TraceWarning("This is a warning message");
        Trace.TraceError("This is an error message");
        Debug.WriteLine("This is a debug message");

        // Structured-ish output
        Trace.TraceInformation($"User: {"AcadUser"}, Action: {"Test"}, Duration: {42}ms");

        // Multi-line
        Trace.TraceInformation("Line 1\nLine 2\nLine 3");

        // Special characters
        Trace.TraceInformation("Special chars: <html> & \"quotes\" 'apostrophe'");

        // Numbers and formatting
        Trace.TraceInformation($"Int: {42}, Double: {3.14159:F2}, Hex: {255:X2}, Currency: {1234.56:C}");

        // Long string
        var longString = new string('A', 1000);
        Trace.TraceInformation($"Long string ({longString.Length} chars): {longString}");

        // Null and empty
        string? nullStr = null;
        Trace.TraceInformation($"Null: {nullStr}, Empty: {""}");

        // Timestamps
        Trace.TraceInformation($"Now: {DateTime.Now:O}");
        Trace.TraceInformation($"UTC: {DateTime.UtcNow:O}");

        Trace.TraceInformation("=== Serilog Formatting Test End ===");
    }
}
