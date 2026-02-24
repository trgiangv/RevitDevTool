using System.Text.Json;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Enums;
using RevitDevTool.Bridge.Json;

namespace RevitDevTool.Console.Services;

/// <summary>
/// Centralizes all configuration operations: parse, validate, merge CLI overrides,
/// and produce a unified <see cref="ExecutionPlan"/>.
/// </summary>
public static class ConfigService
{
    public static BatchConfig ParseConfig(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BatchConfig>(json, BridgeJsonOptions.Instance)
               ?? throw new InvalidOperationException("Failed to parse config file.");
    }

    public static async Task<BatchConfig> ParseConfigAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}");

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<BatchConfig>(json, BridgeJsonOptions.Instance)
               ?? throw new InvalidOperationException("Failed to parse config file.");
    }

    /// <summary>
    /// Merge priority: CLI override > JSON strategy > hardcoded default.
    /// </summary>
    public static ExecutionPlan BuildExecutionPlan(BatchConfig config, CliOverrides overrides)
    {
        var strategy = config.Strategy;

        var processingMode = overrides.ProcessingMode ?? strategy.Mode;
        var parallelCount = overrides.ParallelInstanceCount ?? strategy.ParallelCount;
        var connectionMode = overrides.Launch == true
            ? ConnectionMode.Launch
            : strategy.ConnectionMode;

        var jobs = BatchConfigResolver.Resolve(config);
        BatchConfigValidator.Validate(jobs, processingMode);

        return new ExecutionPlan
        {
            ConnectionMode = connectionMode,
            ProcessingMode = processingMode,
            ParallelInstanceCount = parallelCount,
            LaunchTimeoutSeconds = strategy.LaunchTimeoutSeconds,
            TimeoutPerFileSeconds = strategy.TimeoutPerFileSeconds,
            Jobs = jobs
        };
    }

    public static BatchConfig CreateSample(string appId = "revit")
    {
        return new BatchConfig
        {
            Strategy = new ExecutionStrategy
            {
                ConnectionMode = ConnectionMode.Attach,
                Mode = ProcessingMode.SequentialMulti,
                ParallelCount = 2,
                LaunchTimeoutSeconds = 120,
                TimeoutPerFileSeconds = 1800
            },
            Defaults = new JobDefaults
            {
                HostVersion = appId == "revit" ? "2025" : null,
                Script = "path/to/script.py",
                Headless = true,
                CloseDocument = true,
                CloseHost = false
            },
            Files =
            [
                new FileEntry { Path = "path/to/file1.rvt" },
                new FileEntry
                {
                    Path = "path/to/file2.rvt",
                    HostVersion = "2025",
                    Script = "path/to/other_script.py",
                    Headless = false,
                    CloseDocument = false
                }
            ]
        };
    }

    public static string SerializeSample(string appId = "revit")
    {
        var sample = CreateSample(appId);
        return JsonSerializer.Serialize(sample, BridgeJsonOptions.Indented);
    }
}
