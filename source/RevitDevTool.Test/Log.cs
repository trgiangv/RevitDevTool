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

/// <summary>
/// Comprehensive test for Serilog RichTextBox formatting via Trace/Debug/Console.
/// Tests all scenarios from Serilog.Sinks.RichTextBox.WinForms.Colored Demo.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class SerilogFormattingTestCmd : ExternalCommand
{
    public override void Execute()
    {
        // ═══════════════════════════════════════════════════════════════════
        // SECTION 0: LOG LEVEL DETECTION (via keywords in message)
        // ═══════════════════════════════════════════════════════════════════
        Trace.WriteLine("═══ LOG LEVEL DETECTION TESTS ═══");
        
        // Prefix detection (highest priority)
        Trace.WriteLine("[INFO] This should be Information level");
        Trace.WriteLine("[WARN] This should be Warning level");
        Trace.WriteLine("[ERROR] This should be Error level");
        Trace.WriteLine("[FATAL] This should be Critical level");
        Trace.WriteLine("[DEBUG] This should be Debug level");
        
        // Keyword detection (fallback)
        Trace.WriteLine("Operation completed successfully");  // contains "completed" -> Info
        Trace.WriteLine("Warning: Memory usage is high");     // contains "warning" -> Warning
        Trace.WriteLine("Error occurred during processing");  // contains "error" -> Error
        Trace.WriteLine("Fatal crash detected in system");    // contains "fatal" -> Critical
        Trace.WriteLine("Just a regular debug message");      // no keywords -> Debug
        
        // ═══════════════════════════════════════════════════════════════════
        // SECTION 1: TRACE - All Log Levels (explicit)
        // ═══════════════════════════════════════════════════════════════════
        Trace.WriteLine("═══ TRACE TESTS ═══");
        
        // 1.1 Plain messages - different levels
        Trace.TraceInformation("Plain INFO message");
        Trace.TraceWarning("Plain WARNING message");
        Trace.TraceError("Plain ERROR message");
        
        // 1.2 Scalar values with placeholders
        Trace.TraceInformation("Cache hit ratio: {0:P2}", 0.856);
        Trace.TraceInformation("Response time: {0}", DateTime.Now);
        Trace.TraceInformation("Is valid: {0}", true);
        Trace.TraceInformation("API version: {0}", "2.1.0");
        Trace.TraceWarning("Memory usage: {0} MB (threshold: {1} MB)", 1024, 2048);
        Trace.TraceError("Failed order {0} with code {1}", "ORD-12345", "E500");
        
        // 1.3 Simple object via WriteLine (BEST for objects)
        var simpleMetrics = new { CPU = 85.5, Memory = 1024, Connections = 42 };
        Trace.WriteLine(simpleMetrics);
        
        // 1.4 Complex nested object
        var apiRequest = new
        {
            Method = "POST",
            Endpoint = "/api/users",
            RequestId = Guid.NewGuid(),
            Headers = new { ContentType = "application/json", Authorization = "Bearer ***" }
        };
        Trace.WriteLine(apiRequest);
        
        // 1.5 User activity object
        var userAction = new
        {
            UserId = "user123",
            Action = "Login",
            IP = "192.168.1.1",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = DateTime.UtcNow
        };
        Trace.WriteLine(userAction);
        
        // 1.6 Dictionary-like config
        var config = new
        {
            ConnectionTimeout = 30,
            MaxRetries = 3,
            EnableCompression = true,
            AllowedOrigins = new[] { "https://api.example.com", "https://admin.example.com" }
        };
        Trace.WriteLine(config);
        
        // 1.7 Deep nested structure (audit log)
        var auditLog = new
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            User = new { Id = "user456", Role = "Administrator", Department = "IT" },
            Action = "ConfigurationUpdate",
            Changes = new object[]
            {
                new { Property = "MaxConnections", OldValue = 100, NewValue = 200 },
                new { Property = "Timeout", OldValue = 30, NewValue = 60 }
            }
        };
        Trace.WriteLine(auditLog);
        
        // 1.8 Deployment info (very complex)
        var deploymentInfo = new
        {
            DeploymentId = Guid.NewGuid(),
            Environment = "Production",
            Version = "2.1.0",
            Timestamp = DateTime.UtcNow,
            Services = new object[]
            {
                new
                {
                    Name = "API",
                    Status = "Healthy",
                    Metrics = new { ResponseTime = 45, ErrorRate = 0.01, RequestsPerSecond = 150 }
                },
                new
                {
                    Name = "Database",
                    Status = "Degraded",
                    Metrics = new { ConnectionCount = 85, QueryTime = 120, ReplicationLag = 5 }
                }
            },
            Infrastructure = new
            {
                Region = "us-east-1",
                InstanceType = "t3.large",
                Scaling = new { MinInstances = 2, MaxInstances = 5, CurrentInstances = 3 }
            }
        };
        Trace.WriteLine(deploymentInfo);
        
        // ═══════════════════════════════════════════════════════════════════
        // SECTION 2: DEBUG - Same patterns
        // ═══════════════════════════════════════════════════════════════════
        Debug.WriteLine("═══ DEBUG TESTS ═══");
        
        // 2.1 Plain message
        Debug.WriteLine("Plain debug message");
        
        // 2.2 Object via WriteLine
        var debugQuery = new
        {
            Sql = "SELECT * FROM Users WHERE Status = @status",
            Parameters = new { status = "Active" },
            ExecutionTime = 150
        };
        Debug.WriteLine(debugQuery);
        
        // ═══════════════════════════════════════════════════════════════════
        // SECTION 3: CONSOLE - Same patterns
        // ═══════════════════════════════════════════════════════════════════
        Console.WriteLine("═══ CONSOLE TESTS ═══");
        
        // 3.1 Plain message
        Console.WriteLine("Plain console message");
        
        // 3.2 Object via WriteLine
        var consoleMetrics = new { Uptime = TimeSpan.FromHours(48.5), Requests = 1000000 };
        Console.WriteLine(consoleMetrics);
        
        // ═══════════════════════════════════════════════════════════════════
        // SECTION 4: EXCEPTION HANDLING
        // ═══════════════════════════════════════════════════════════════════
        Debug.WriteLine("═══ EXCEPTION TEST ═══");
        try
        {
            throw new InvalidOperationException("Test exception", new Exception("Inner exception"));
        }
        catch (Exception ex)
        {
            Trace.TraceError("Exception caught: {0}", ex.Message);
            Debug.WriteLine(ex);
        }
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

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchDebugUrlsCmd : ExternalCommand
{
    public override void Execute()
    {
        var urls = new[]
        {
            "http://example.com/resource1",
            "http://example.com/resource2",
            "http://example.com/resource3"
        };

        foreach (var url in urls)
        {
            Debug.WriteLine($"Accessing URL: {url}");
        }
    }
}

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class BatchDebugPrettyJsonCmd : ExternalCommand
{
    public override void Execute()
    {
        var auditLog = new
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            User = new
            {
                Id = "user456",
                Role = "Administrator",
                Department = "IT"
            },
            Action = "ConfigurationUpdate",
            Changes = new[]
            {
                new { Property = "MaxConnections", OldValue = 100, NewValue = 200 },
                new { Property = "Timeout", OldValue = 30, NewValue = 60 }
            } as object[]
        };
        
        Trace.WriteLine(auditLog);
    }
}