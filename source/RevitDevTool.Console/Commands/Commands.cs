using System.Text.Json;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Enums;
using RevitDevTool.Bridge.Json;
using RevitDevTool.Console.RevitFileInfo.TransmissionDataStream;
using RevitDevTool.Console.Services;
using RevitDevTool.Console.Services.Hosting;

namespace RevitDevTool.Console.Commands;

public sealed class Commands
{
    /// <summary>
    /// Execute a batch config against host application instances.
    /// </summary>
    /// <param name="config">-c, Path to batch config JSON file</param>
    /// <param name="mode">-m, Override processing mode: single, multi, parallel</param>
    /// <param name="parallel">-p, Override parallel instance count</param>
    /// <param name="launch">Override connection mode to launch (start new host instances)</param>
    /// <param name="dryRun">Validate config and print resolved plan without executing</param>
    /// <param name="json">Output result as JSON</param>
    /// <param name="cancellationToken"></param>
    public async Task Run(
        string config,
        string? mode = null,
        int? parallel = null,
        bool launch = false,
        bool dryRun = false,
        bool json = false,
        CancellationToken cancellationToken = default)
    {
        var batchConfig = await ConfigService.ParseConfigAsync(config, cancellationToken).ConfigureAwait(false);

        var overrides = new CliOverrides
        {
            ProcessingMode = mode != null ? ParseProcessingMode(mode) : null,
            ParallelInstanceCount = parallel,
            Launch = launch ? true : null
        };

        var plan = ConfigService.BuildExecutionPlan(batchConfig, overrides);

        PrintPlanSummary(plan);

        if (dryRun)
        {
            System.Console.WriteLine("\n--dry-run: exiting without execution.");
            return;
        }

        var discovery = new RevitDiscovery();
        var launcher = new RevitLauncher();
        await using var runner = new BatchRunner(discovery, launcher);
        runner.OnProgress += (instance, progress) =>
            System.Console.WriteLine(
                $"  [{instance.HostVersion}:{instance.ProcessId}] {progress.Message} ({progress.Current}/{progress.Total})");

        try
        {
            var result = await runner.RunAsync(plan, cancellationToken).ConfigureAwait(false);

            if (json)
                System.Console.WriteLine(JsonSerializer.Serialize(result, BridgeJsonOptions.Indented));
            else
                PrintBatchResult(result);

            Environment.ExitCode = result.FailureCount > 0 ? 1 : 0;
        }
        catch (InvalidOperationException ex)
        {
            await System.Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// List running host instances and installed versions.
    /// </summary>
    public Task Status()
    {
        var discovery = new RevitDiscovery();
        var instances = discovery.Discover();

        if (instances.Count == 0)
        {
            System.Console.WriteLine("No running Revit instances with engine pipe found.");
        }
        else
        {
            System.Console.WriteLine("Running instances:");
            foreach (var inst in instances)
                System.Console.WriteLine($"  {inst.AppId} {inst.HostVersion}  PID {inst.ProcessId}  [{inst.PipeName}]");
        }

        var scanner = new RevitVersionScanner();
        var installed = scanner.GetInstalledVersions();
        System.Console.WriteLine(
            $"\nInstalled Revit versions: {(installed.Count > 0 ? string.Join(", ", installed) : "(none found)")}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Show file info (Revit version, worksharing, links) for all files in a batch config.
    /// </summary>
    /// <param name="config">-c, Path to batch config JSON file</param>
    public Task Info(string config)
    {
        var batchConfig = ConfigService.ParseConfig(config);

        if (batchConfig.Files.Count == 0)
        {
            System.Console.WriteLine("No files found in config.");
            return Task.CompletedTask;
        }

        foreach (var file in batchConfig.Files)
            PrintFileInfo(file.Path);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generate a sample batch config JSON and print to stdout.
    /// </summary>
    /// <param name="app">Host application id (default: revit)</param>
    public Task Sample(string app = "revit")
    {
        System.Console.WriteLine(ConfigService.SerializeSample(app));
        return Task.CompletedTask;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static readonly HashSet<ExternalFileReferenceType> LinkTypes =
    [
        ExternalFileReferenceType.RevitLink,
        ExternalFileReferenceType.CADLink,
        ExternalFileReferenceType.DWFMarkup,
        ExternalFileReferenceType.Decal
    ];

    private static void PrintFileInfo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            System.Console.WriteLine($"  {filePath}: NOT FOUND");
            return;
        }

        try
        {
            var info = new RevitFileInfo.RevitFileInfo(filePath);
            var year = info.GetRevitYear();
            var ws = info.BasicFileInfo?.WorksharingType;
            var central = info.BasicFileInfo?.CentralPath;

            var allRefs = info.TransmissionData?.ExternalFileReferences ?? [];
            var links = allRefs.Where(r => LinkTypes.Contains(r.ExternalFileReferenceType)).ToList();

            System.Console.WriteLine($"  {filePath}");
            System.Console.WriteLine($"    Version: Revit {year ?? 0}");
            System.Console.WriteLine($"    Worksharing: {ws}");
            if (!string.IsNullOrEmpty(central))
                System.Console.WriteLine($"    Central: {central}");
            System.Console.WriteLine($"    External links: {links.Count}");

            foreach (var link in links)
                System.Console.WriteLine($"      [{link.ExternalFileReferenceType}] {link.LastSavedAbsolutePath ?? link.LastSavedPath}");

            var nonLinks = allRefs.Where(r => !LinkTypes.Contains(r.ExternalFileReferenceType)).ToList();
            if (nonLinks.Count > 0)
            {
                System.Console.WriteLine($"    Other references: {nonLinks.Count}");
                foreach (var r in nonLinks)
                    System.Console.WriteLine($"      [{r.ExternalFileReferenceType}] {r.LastSavedAbsolutePath ?? r.LastSavedPath}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  {filePath}: ERROR - {ex.Message}");
        }
    }

    private static ProcessingMode ParseProcessingMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "sequentialsingle" or "single" => ProcessingMode.SequentialSingle,
            "sequentialmulti" or "multi" => ProcessingMode.SequentialMulti,
            "parallel" => ProcessingMode.Parallel,
            _ => throw new ArgumentException(
                $"Unknown processing mode: '{mode}'. Use: single, multi, parallel")
        };
    }

    private static void PrintPlanSummary(ExecutionPlan plan)
    {
        System.Console.WriteLine(
            $"Resolved {plan.Jobs.Count} jobs, mode: {plan.ProcessingMode}, " +
            $"connection: {plan.ConnectionMode.ToString().ToLowerInvariant()}");

        foreach (var job in plan.Jobs)
        {
            var flags = $"headless={job.Open.Headless}, closeDoc={job.Lifecycle.CloseDocument}, closeHost={job.Lifecycle.CloseHost}";
            System.Console.WriteLine($"  {job.FilePath} -> v{job.HostVersion} ({flags})");
        }
    }

    private static void PrintBatchResult(BatchResult result)
    {
        System.Console.WriteLine();
        System.Console.WriteLine("=== Batch Result ===");
        System.Console.WriteLine($"Total: {result.TotalFiles}  Success: {result.SuccessCount}  Failed: {result.FailureCount}");
        System.Console.WriteLine($"Duration: {result.TotalDurationMs}ms");

        for (var i = 0; i < result.Results.Count; i++)
        {
            var r = result.Results[i];
            var status = r.Success ? "OK" : "FAIL";
            System.Console.WriteLine($"  [{i + 1}] {status} ({r.DurationMs}ms)");
            if (r.Error == null) continue;
            System.Console.WriteLine($"      Error: {r.Error}");
            if (r.StackTrace != null)
                System.Console.WriteLine($"      Stack: {r.StackTrace}");
        }
    }
}
