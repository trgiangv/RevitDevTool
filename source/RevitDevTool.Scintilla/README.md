# RevitDevTool.Scintilla

High-throughput log viewer core for WinForms `Scintilla` with logger-agnostic ingest contracts.

## What this project provides

- `LogEntry` model and ingest contract (`ILogIngress`) independent from Serilog/ZLogger.
- Bounded channel pipeline with periodic UI flush in `ScintillaLogViewerController`.
- `MaxLines` retention with chunk trimming to avoid unbounded memory growth.
- Basic level/text filtering and next-match search.
- WinForms host wrapper (`ScintillaLogViewerHost`) for fast integration.

## Quick integration

```csharp
var host = new ScintillaLogViewerHost(new ScintillaLogViewerOptions
{
    ChannelCapacity = 50_000,
    FlushIntervalMs = 50,
    MaxBatchSize = 800,
    MaxLines = 100_000
});

parentPanel.Controls.Add(host.HostControl);
host.Controller.Start();
```

Push logs from any logger adapter:

```csharp
host.Controller.TryPost(new LogEntry
{
    TimestampUtc = DateTime.UtcNow,
    Level = LogSeverity.Information,
    Message = "Hello from adapter",
    Source = "Demo"
});
```

Or register directly into `Microsoft.Extensions.Logging`:

```csharp
builder.Logging
    .ClearProviders()
    .AddZLoggerScintilla(
        sp => sp.GetRequiredService<ScintillaLogViewerHost>().Controller,
        options => options.MinimumLevel = LogLevel.Information)
    .AddZLoggerRollingFile((date, index) => $"logs/{date:yyyyMMdd}-{index}.log", RollingInterval.Day);
```

## Performance defaults

- `FlushIntervalMs = 75`
- `MaxBatchSize = 500`
- `ChannelCapacity = 20_000`
- `DropPolicy = DropOldest`
- `MaxLines = 50_000`
- `TrimChunkLines = 1_000`

## Benchmark checklist

- Flood test 1-5 minutes with mixed `Information/Warning/Error`.
- Verify UI remains responsive while writing.
- Confirm dropped message metric behavior under pressure.
- Confirm line count never exceeds `MaxLines` for long runs.
- Validate search/filter latency on large documents.
