# Log Output Reference

Examples of log output for each logging API, showing both file log and monitor (Scintilla) display.

Last updated: 2026-07-01

---

## ILogger<T> (MEL direct, via ZLogger)

| API | File Output | Monitor Output |
|-----|------------|----------------|
| `logger.ZLogDebug("loaded 3 assemblies")` | `[14:22:05 DBG] [MyAddin.Loader] loaded 3 assemblies` | `[MyAddin.Loader] loaded 3 assemblies` |
| `logger.ZLogInformation("startup complete")` | `[14:22:05 INF] [MyAddin.Loader] startup complete` | `[MyAddin.Loader] startup complete` |
| `logger.ZLogWarning("config key missing")` | `[14:22:05 WRN] [MyAddin.Loader] config key missing` | `[MyAddin.Loader] config key missing` |
| `logger.ZLogError(ex, "failed to load")` | `[14:22:05 ERR] [MyAddin.Loader] failed to load` | `[MyAddin.Loader] failed to load` |
| `logger.ZLogCritical(ex, "corrupted state")` | `[14:22:05 CRT] [MyAddin.Loader] corrupted state` | `[MyAddin.Loader] corrupted state` |

**Monitor** includes `[FullClassName]` as ZLogger default prefix.
**File** adds `[timestamp level]` prefix; `[FullClassName]` is conditionally included for MEL-direct entries (non-empty `CategoryName`).

---

## Trace.WriteLine / Debug.WriteLine

| API | File Output | Monitor Output | Level |
|-----|------------|----------------|-------|
| `Trace.WriteLine("processing file")` | `[14:22:05 DBG] processing file` | `processing file` | Debug* |
| `Trace.WriteLine("ERROR: file not found")` | `[14:22:05 ERR] ERROR: file not found` | `ERROR: file not found` | Error* |
| `Trace.WriteLine("processing", "MyAddin")` | `[14:22:05 DBG] [MyAddin] processing` | `[MyAddin] processing` | Debug* |
| `Debug.WriteLine("debug info")` | `[14:22:05 DBG] debug info` | `debug info` | Debug* |

*Level detected by `LogLevelDetector` scanning message content for keywords (`error`, `warning`, `critical`, etc.). Falls back to `Debug`.

`Trace.WriteLine(msg, category)` → `LoggerTraceListener.Write(msg, category)` prepends `[category]` to the message before routing to MEL.

---

## Trace.TraceError / TraceWarning / TraceInformation

| API | File Output | Monitor Output | Level |
|-----|------------|----------------|-------|
| `Trace.TraceError("export failed")` | `[14:22:05 ERR] export failed` | `export failed` | Error |
| `Trace.TraceWarning("low memory")` | `[14:22:05 WRN] low memory` | `low memory` | Warning |
| `Trace.TraceInformation("sync done")` | `[14:22:05 INF] sync done` | `sync done` | Info |
| `Trace.TraceError("item {0} missing", "X")` | `[14:22:05 ERR] item X missing` | `item X missing` | Error |

Level is determined by `TraceEventType`, not message content. `TraceError` → `Error`, `TraceWarning` → `Warning`, etc.

---

## Console.WriteLine (captured by ConsoleRedirector)

| API | File Output | Monitor Output | Level |
|-----|------------|----------------|-------|
| `Console.WriteLine("hello")` | `[14:22:05 DBG] hello` | `hello` | Debug* |
| `Console.WriteLine("ERROR: crash")` | `[14:22:05 ERR] ERROR: crash` | `ERROR: crash` | Error* |
| `Console.Error.WriteLine("fail")` | `[14:22:05 DBG] fail` | `fail` | Debug* |

*Level detected by `LogLevelDetector`.

`ConsoleRedirector` redirects `Console.Out` and `Console.Error` to `Trace.Write()`, which then flows through `LoggerTraceListener`.

---

## Third-Party Addin (ZLoggerInMemory → Trace)

```csharp
// Setup
services.AddZLoggerInMemory(processor =>
    processor.MessageReceived += msg =>
        System.Diagnostics.Trace.WriteLine(msg));

// Prefix formatter includes level so LogLevelDetector can map correctly:
// $"[{0:short}] [{1}] " → "[DBG] [MyAddin.Loader] "
```

| Usage | File Output | Monitor Output |
|-------|------------|----------------|
| `logger.ZLogDebug("loaded")` | `[14:22:05 DBG] [MyAddin.Loader] loaded` | `[MyAddin.Loader] loaded` |
| `logger.ZLogError(ex, "failed")` | `[14:22:05 ERR] [MyAddin.Loader] failed` | `[MyAddin.Loader] failed` |

**Important:** The `$[{0:short}]` level prefix in the formatter is required. `Trace.WriteLine` does not carry level metadata, so `LogLevelDetector` relies on level prefixes (`[DBG]`, `[ERR]`, `[WRN]`, etc.) to determine the correct MEL log level from the message content.
