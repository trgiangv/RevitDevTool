namespace FSharpDemo

open System
open System.Diagnostics
open System.Threading.Tasks
open Autodesk.Revit.Attributes
open JetBrains.Annotations
open Nice3point.Revit.Toolkit.External

[<UsedImplicitly>]
[<Transaction(TransactionMode.Manual)>]
type BatchDebugLogCmd() =
    inherit ExternalCommand()

    override _.Execute() =
        Task.Run(fun () ->
            let stopwatch = Stopwatch.StartNew()
            for stepNumber in 1 .. 10000 do
                let s = stepNumber.ToString("000")
                Debug.WriteLine($"Processing batch item Step {stepNumber}")
                Debug.WriteLine($"Batch processing metrics for Step {stepNumber}")
                Debug.WriteLine($"Completed processing Step {s}")
                Debug.WriteLine($"Performance warning for Step {s}")
                Debug.WriteLine($"Failed to process Step {s}")
                Debug.WriteLine($"Critical failure in Step {s}")
            stopwatch.Stop()
            Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms")
        ) |> ignore

[<UsedImplicitly>]
[<Transaction(TransactionMode.Manual)>]
type BatchTraceLogCmd() =
    inherit ExternalCommand()

    override _.Execute() =
        Task.Run(fun () ->
            let stopwatch = Stopwatch.StartNew()
            for stepNumber in 1 .. 10000 do
                let s = stepNumber.ToString("000")
                Trace.WriteLine($"Processing batch item Step {stepNumber}")
                Trace.WriteLine($"Batch processing metrics for Step {stepNumber}")
                Trace.WriteLine($"Completed processing Step {s}")
                Trace.WriteLine($"Performance warning for Step {s}")
                Trace.WriteLine($"Failed to process Step {s}")
                Trace.WriteLine($"Critical failure in Step {s}")
            stopwatch.Stop()
            Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms")
        ) |> ignore

[<UsedImplicitly>]
[<Transaction(TransactionMode.Manual)>]
type BatchConsoleLogCmd() =
    inherit ExternalCommand()

    override _.Execute() =
        Task.Run(fun () ->
            let stopwatch = Stopwatch.StartNew()
            for stepNumber in 1 .. 10000 do
                let s = stepNumber.ToString("000")
                Console.WriteLine($"Processing batch item Step {stepNumber}")
                Console.WriteLine($"Batch processing metrics for Step {stepNumber}")
                Console.WriteLine($"Completed processing Step {s}")
                Console.WriteLine($"Performance warning for Step {s}")
                Console.WriteLine($"Failed to process Step {s}")
                Console.WriteLine($"Critical failure in Step {s}")
            stopwatch.Stop()
            Trace.TraceWarning($"Total processing time: {stopwatch.ElapsedMilliseconds} ms")
        ) |> ignore

/// Comprehensive test for Serilog RichTextBox formatting via Trace/Debug/Console.
/// Tests all scenarios from Serilog.Sinks.RichTextBox.WinForms.Colored Demo.
[<UsedImplicitly>]
[<Transaction(TransactionMode.Manual)>]
type SerilogFormattingTestCmd() =
    inherit ExternalCommand()

    override _.Execute() =
        // ═══════════════════════════════════════════════════════════════════
        // SECTION 0: LOG LEVEL DETECTION (via keywords in message)
        // ═══════════════════════════════════════════════════════════════════
        Trace.WriteLine("═══ LOG LEVEL DETECTION TESTS ═══")

        Trace.WriteLine("[INFO] This should be Information level")
        Trace.WriteLine("[WARN] This should be Warning level")
        Trace.WriteLine("[ERROR] This should be Error level")
        Trace.WriteLine("[FATAL] This should be Critical level")
        Trace.WriteLine("[DEBUG] This should be Debug level")

        Trace.WriteLine("Operation completed successfully")
        Trace.WriteLine("Warning: Memory usage is high")
        Trace.WriteLine("Error occurred during processing")
        Trace.WriteLine("Fatal crash detected in system")
        Trace.WriteLine("Just a regular debug message")

        // ═══════════════════════════════════════════════════════════════════
        // SECTION 1: TRACE - All Log Levels (explicit)
        // ═══════════════════════════════════════════════════════════════════
        Trace.WriteLine("═══ TRACE TESTS ═══")

        Trace.TraceInformation("Plain INFO message")
        Trace.TraceWarning("Plain WARNING message")
        Trace.TraceError("Plain ERROR message")

        Trace.TraceInformation("Cache hit ratio: {0:P2}", 0.856)
        Trace.TraceInformation("Response time: {0}", DateTime.Now)
        Trace.TraceInformation("Is valid: {0}", true)
        Trace.TraceInformation("API version: {0}", "2.1.0")
        Trace.TraceWarning("Memory usage: {0} MB (threshold: {1} MB)", 1024, 2048)
        Trace.TraceError("Failed order {0} with code {1}", "ORD-12345", "E500")

        let simpleMetrics = {| CPU = 85.5; Memory = 1024; Connections = 42 |}
        Trace.WriteLine(simpleMetrics)

        let apiRequest =
            {| Method = "POST"
               Endpoint = "/api/users"
               RequestId = Guid.NewGuid()
               Headers = {| ContentType = "application/json"; Authorization = "Bearer ***" |} |}
        Trace.WriteLine(apiRequest)

        let userAction =
            {| UserId = "user123"
               Action = "Login"
               IP = "192.168.1.1"
               UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
               Timestamp = DateTime.UtcNow |}
        Trace.WriteLine(userAction)

        let config =
            {| ConnectionTimeout = 30
               MaxRetries = 3
               EnableCompression = true
               AllowedOrigins = [| "https://api.example.com"; "https://admin.example.com" |] |}
        Trace.WriteLine(config)

        let auditLog =
            {| EventId = Guid.NewGuid()
               Timestamp = DateTime.UtcNow
               User = {| Id = "user456"; Role = "Administrator"; Department = "IT" |}
               Action = "ConfigurationUpdate"
               Changes =
                [| {| Property = "MaxConnections"; OldValue = 100; NewValue = 200 |}
                   {| Property = "Timeout"; OldValue = 30; NewValue = 60 |} |] |}
        Trace.WriteLine(auditLog)

        let deploymentInfo =
            {| DeploymentId = Guid.NewGuid()
               Environment = "Production"
               Version = "2.1.0"
               Timestamp = DateTime.UtcNow
               Services =
                [| box {| Name = "API"; Status = "Healthy"
                          Metrics = {| ResponseTime = 45; ErrorRate = 0.01; RequestsPerSecond = 150 |} |}
                   box {| Name = "Database"; Status = "Degraded"
                          Metrics = {| ConnectionCount = 85; QueryTime = 120; ReplicationLag = 5 |} |} |]
               Infrastructure =
                {| Region = "us-east-1"; InstanceType = "t3.large"
                   Scaling = {| MinInstances = 2; MaxInstances = 5; CurrentInstances = 3 |} |} |}
        Trace.WriteLine(deploymentInfo)

        // ═══════════════════════════════════════════════════════════════════
        // SECTION 2: DEBUG - Same patterns
        // ═══════════════════════════════════════════════════════════════════
        Debug.WriteLine("═══ DEBUG TESTS ═══")

        Debug.WriteLine("Plain debug message")

        let debugQuery =
            {| Sql = "SELECT * FROM Users WHERE Status = @status"
               Parameters = {| status = "Active" |}
               ExecutionTime = 150 |}
        Debug.WriteLine(debugQuery)

        // ═══════════════════════════════════════════════════════════════════
        // SECTION 3: CONSOLE - Same patterns
        // ═══════════════════════════════════════════════════════════════════
        Console.WriteLine("═══ CONSOLE TESTS ═══")

        Console.WriteLine("Plain console message")

        let consoleMetrics = {| Uptime = TimeSpan.FromHours(48.5); Requests = 1000000 |}
        Console.WriteLine(consoleMetrics)

        // ═══════════════════════════════════════════════════════════════════
        // SECTION 4: EXCEPTION HANDLING
        // ═══════════════════════════════════════════════════════════════════
        Debug.WriteLine("═══ EXCEPTION TEST ═══")
        try
            raise (InvalidOperationException("Test exception", Exception("Inner exception")))
        with
        | ex ->
            Trace.TraceError("Exception caught: {0}", ex.Message)
            Debug.WriteLine(ex)

[<UsedImplicitly>]
[<Transaction(TransactionMode.Manual)>]
type BatchDebugLargeStringCmd() =
    inherit ExternalCommand()

    override _.Execute() =
        Task.Run(fun () ->
            let stopwatch = Stopwatch.StartNew()
            let largeString = String('X', 1000)
            // Trace.TraceWarning("largeString")
            for _ in 1 .. 1000 do
                Debug.WriteLine(largeString)
            stopwatch.Stop()
            Trace.TraceWarning($"Total time for large string logging: {stopwatch.ElapsedMilliseconds} ms")
        ) |> ignore

[<UsedImplicitly>]
[<Transaction(TransactionMode.Manual)>]
type BatchDebugUrlsCmd() =
    inherit ExternalCommand()

    override _.Execute() =
        let urls =
            [| "https://example.com/resource1"
               "https://example.com/resource2"
               "https://example.com/resource3" |]
        for url in urls do
            Debug.WriteLine($"Accessing URL: {url}")

[<UsedImplicitly>]
[<Transaction(TransactionMode.Manual)>]
type BatchDebugPrettyJsonCmd() =
    inherit ExternalCommand()

    override _.Execute() =
        let auditLog =
            {| EventId = Guid.NewGuid()
               Timestamp = DateTime.UtcNow
               User = {| Id = "user456"; Role = "Administrator"; Department = "IT" |}
               Action = "ConfigurationUpdate"
               Changes =
                [| {| Property = "MaxConnections"; OldValue = 100; NewValue = 200 |}
                   {| Property = "Timeout"; OldValue = 30; NewValue = 60 |} |] |}
        Trace.WriteLine(auditLog)

