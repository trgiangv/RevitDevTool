# RevitDevTool.Scintilla

Standalone high-throughput log viewer built on `Scintilla.NET`, optimized for `ZLogger` + UTF-8 direct pipeline.

## Projects

- `RevitDevTool.Scintilla`: core library (`net48`, `net8.0-windows`).
- `RevitDevTool.Scintilla.Demo`: WinForms demo.
- `RevitDevTool.Scintilla.Wpf.Demo`: WPF demo.
- `RevitDevTool.Scintilla.Benchmarks`: throughput/allocation benchmarks.

## Benchmark Methodology

- `FullPipeline`: ingest + filter/search + forced pixel draw hash.
- `Core`: ingest/filter/search logic without paint cost.
- `Pixel`: draw-only cost for prepared controls.
- Benchmark datasets are seed-based deterministic to keep regression comparisons stable.

## Design Constraints

- Public ingest path is **ZLogger only** through `AddZLoggerScintilla(...)`.
- No public direct `LogEntry` post API.
- Public `ILogRenderStrategy` surface is intentionally minimal (style mapping/config only).
- UTF-8 is first-class in ingest/filter/render hot paths.
- URL detection uses shared `UrlScanner` span scanner (no regex in production hot path).

## Module Layout

- `Core`: shared models/options/themes (`LogEntry`, `RenderSegment`, `ScintillaTheme`, `ScintillaLogViewerOptions`).
- `Control`: public viewer surface (`ScintillaLogViewer`, `ScintillaLogViewerWpf`, controller/event contracts).
- `Logger`: ingest pump, controller runtime, ZLogger processor bridge.
- `Formatting`: UTF-8/token/json formatting pipeline.
- `Render`: style/theme provider and render strategy contracts/implementation.
- `Services`: Scintilla document append/search/style/interaction internals.
- `Search`: filter/search engine + models.
- `Extensions`: DI and logging registration extensions.
- `Helpers` + `Internal`: shared utilities and internal keys.

## Formatting Pipeline

- `RenderOrchestrator`: entry orchestration for line rewrite and segment rendering.
- `JsonValueFormatter`: pretty-json parse + semantic JSON token styling.
- `DisplayValueFormatter`: non-json tokenization and object-like message styling.
- `TokenResolutionEngine`: centralized callbacks/classifier token resolution.
- `PayloadFormattingHelpers`: shared structured payload metadata + URL payload helpers.

## DI Wiring (WinForms)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Contracts.Core;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Logger;
using RevitDevTool.Scintilla.Render;
using ZLogger;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddScintillaLogViewerWinForms(_ => new ScintillaLogViewerOptions
{
    Theme = ScintillaTheme.EnhancedDark,
    ChannelCapacity = 50_000,
    MaxLines = 50_000,
    MaxHistoryEntries = 50_000,
    MaxBatchSize = 800,
    FlushIntervalMs = 50
});

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Trace);
builder.Logging.AddZLoggerScintilla(zlogger =>
{
    zlogger.IncludeScopes = true;
    zlogger.UsePlainTextFormatter(formatter =>
    {
        formatter.SetPrefixFormatter(
            $"[{0:local-timeonly} {1:short}] ",
            (in template, in info) => template.Format(info.Timestamp, info.LogLevel));
    });
});

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Demo");
logger.ZLogInformation($"Viewer ready");
```

`AddZLoggerScintilla()` resolves `IScintillaLogViewHost` and `ILogViewerControlEvents` from DI by default. Register `AddScintillaLogViewerWinForms(...)` or `AddScintillaLogViewerWpf(...)` first. Available overloads:

- `AddZLoggerScintilla()`
- `AddZLoggerScintilla(Action<ZLoggerOptions> configureZLogger)`
- `AddZLoggerScintilla(Action<ScintillaRegistrationOptions> configure)` for advanced custom binding.

Host registration helpers:

- `AddScintillaLogViewerWinForms(...)`
- `AddScintillaLogViewerWpf(...)`

## WPF Host

Use `ScintillaLogViewerWpf` as `IScintillaLogViewHost` and register `ILogViewerControlEvents` the same way as WinForms.

## Runtime Control Without Reset

Use `ILogViewerControlEvents` (`RequestStart`, `RequestStop`, `RequestClear`, `RequestSetAutoScroll`, `RequestFilter`, `RequestSearch`, `RequestTheme`, `RequestRenderMode`) to control UI behavior while keeping the same logger/provider pipeline alive. Subscribe in your host UI to `StartRequested`, `StopRequested`, `ClearRequested`, `AutoScrollChanged`, `FilterRequested`, `SearchRequested`, `ThemeChanged`, and `RenderModeChanged` as needed.

## Runtime Checklist

- Flood logging for 1-5 minutes and verify UI responsiveness.
- Verify bounded memory with `MaxLines`, `MaxHistoryEntries`, `TrimChunkLines`.
- Test `Stop`, `Clear(Fast/Aggressive)`, `Filter`, `FindNext`.
- Test pretty-json toggle + URL clickable behavior + multi-object interpolation.
