# Execution Plan: MCP host logging (SDK filters + McpJsonUtilities — delete redundant helpers)

Date: 2026-07-25

## Status

Completed — 2026-07-25

## Outcome

1. Host/daemon MCP logging = **SDK request-filter (or handler) + `ILogger`**, with **args JSON kept** from the protocol API.
2. Serialize MCP protocol values with **`McpJsonUtilities.DefaultOptions`** / SDK type info — same utilities the rest of the stack and csharp-sdk already use (`McpPrimitiveDispatcher`, `PythonToolsetParser`, etc.).
3. **Delete redundant Observability helpers** (`McpCallLog`, `McpPayloadFormatter`, invented preview/observe APIs, and call-logging use of `McpActivitySources` unless a probe proves it adds something SDK Diagnostics does not).

No new Preview/Observe/Call framework.

## Decisions (locked)

| Decision | Choice |
|----------|--------|
| Pipeline | SDK filters + `ILogger` (docs/tests pattern) |
| Args | Keep JSON from API (`Arguments` / `JsonElement`) |
| JSON options | **`McpJsonUtilities.DefaultOptions`** (and `JsonContext` type infos where applicable) — do not invent parallel `JsonSerializerOptions` for MCP payloads in this path |
| Preview/Observe/CallLog | **Do not invent**; delete existing call-log stack |
| Redact engine | **Drop** with `McpPayloadFormatter` (was invented). Raw JSON like Inspector unless a *pre-existing* shared scrubber is reused without new types — none required for MVP |
| Binary content in logs | Never dump `Data` base64; describe via type / mime / length (SDK DebuggerDisplay spirit) or serialize result with care so image data is not pasted into monitor lines |
| External telemetry | Out of scope |

## Delete inventory (redundant)

Remove these as part of this work (breaking OK):

| Item | Why redundant |
|------|----------------|
| `source/DevTools.Mcp/Observability/McpCallLog.cs` (entire file: `McpCallLog`, `McpCallFields`, `McpCallOutcome`, `McpPayloadFormatter`, SensitiveKeys, Flatten/Truncate/Summarize/Redact*) | Reimplements filter lifecycle + invents payload pipeline |
| Call-site use of `McpCallLog` / `McpPayloadFormatter` | `HostMcpServerFactory`, `InvokeDynamicTool`, `SearchDynamicTool` |
| Tests that only exercise `McpPayloadFormatter` / CallLog scopes | Rewrite to assert filter/handler `ILogger` output + JSON args |
| `McpActivitySources` **for call logging** | SDK `Diagnostics` / `Experimental.ModelContextProtocol` already owns MCP spans. After probe: delete file + global usings if unused elsewhere; if still referenced for non-log reasons, stop using it from logging path |

**Keep (not logging helpers — different job):**

| Item | Why keep |
|------|----------|
| `ToolHelpers.Result` / `ErrorResult` / session resolve | Tool *response* factories for clients, not log formatting |
| `ToolHelpers.IndentedJsonOptions` | Pretty JSON **inside tool result text** for humans/LLM — separate from monitor logging. Prefer not expanding it; logging uses `McpJsonUtilities.DefaultOptions` (typically compact) |
| `McpJsonUtilities` (package) | Canonical — use more, don’t wrap |

**Do not add:** `McpPayloadPreview`, `McpObserve*`, `IMcp*Sink`, new Observability public surface.

If `Observability/` becomes empty after deletions, remove the folder and the project `<Using Include="DevTools.Mcp.Observability"/>`.

## Approach

### Host filter (canonical)

```csharp
using ModelContextProtocol; // McpJsonUtilities
using ModelContextProtocol.Protocol; // RequestMethods

options.Filters.Request.CallToolFilters.Add(next => async (request, cancellationToken) =>
{
    var logger = toolLogger;
    var target = request.Params?.Name ?? "(unknown)";
    var argsJson = SerializeArgs(request.Params?.Arguments); // JsonElement → JSON via SDK options
    var sw = Stopwatch.StartNew();
    try
    {
        var result = await next(request, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        var resultJson = JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions);
        // Prefer compact line; avoid megabyte base64 — see “Binary” below
        if (result.IsError == true)
            logger.ZLogWarning($"tools/call error target={target} durationMs={sw.ElapsedMilliseconds} args={argsJson} result={resultJson}");
        else
            logger.ZLogInformation($"tools/call ok target={target} durationMs={sw.ElapsedMilliseconds} args={argsJson} result={resultJson}");
        return result;
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        sw.Stop();
        logger.ZLogWarning($"tools/call timeout target={target} durationMs={sw.ElapsedMilliseconds} args={argsJson}");
        throw;
    }
    catch (Exception ex)
    {
        sw.Stop();
        logger.ZLogError(ex, $"tools/call error target={target} durationMs={sw.ElapsedMilliseconds} args={argsJson} error={ex.Message}");
        throw;
    }
});
```

`SerializeArgs`: build JSON object from `IReadOnlyDictionary<string, JsonElement>` using **existing** JSON APIs / `McpJsonUtilities.DefaultOptions` — no redact/flatten/summarize type. Same for `resources/read` with `RequestMethods.ResourcesRead` + URI.

### Binary / huge results (no Preview type)

When logging `CallToolResult` / `ReadResourceResult`:

- Prefer `JsonSerializer.Serialize(..., McpJsonUtilities.DefaultOptions)` **only if** content is text-sized.
- If content includes `ImageContentBlock` / `AudioContentBlock` / blob: **do not** put `Data` on the monitor line — either skip those properties when writing the log string (minimal local logic in the filter file) or log `type` + mime + length only for those blocks. This mirrors SDK DebuggerDisplay, not a new utility product.

Optional soft clamp on the **final log string** length (local const in the filter file) to protect the monitor — not a shared Truncate helper class.

### Daemon

Same: drop `McpCallLog`; `ILogger` + `McpJsonUtilities` around invoke/search. Method string = `tools/call` when that is what ran.

### Docs

- `docs/product/mcp.md` — fix hash-only sentence; host logs via filters + protocol JSON.
- `docs/architecture/MCP/tools.md` — replace Call Logging section; cite `McpJsonUtilities`, delete references to `McpCallLog.cs`.

## Soft knobs (optional feedback)

1. Final log-line clamp: **none** / **4k** / **2k** (recommend **4k** soft cap on the composed message only).
2. Confirm delete `McpActivitySources.cs` entirely if unused after probe.

## Progress

- [x] Align with SDK filters; no Observe/Call/Preview framework
- [x] Keep args JSON from protocol API
- [x] Mandate `McpJsonUtilities.DefaultOptions`; delete redundant Observability helpers
- [x] Implement deletion + filter/handler logging
- [x] Rewrite tests
- [x] Update product + architecture docs
- [x] → `docs/plans/completed/`

## Implementation tasks

1. **Delete** `McpCallLog.cs`; purge references; probe/delete `McpActivitySources` as applicable; clean global usings / empty folder.
2. **Host** `HostMcpServerFactory`: SDK-style filter logging with `McpJsonUtilities.DefaultOptions`.
3. **Daemon** `InvokeDynamicTool` / `SearchDynamicTool`: same.
4. **Tests:** remove formatter-only tests; assert logger messages contain `tools/call` / target / args JSON; no dependency on deleted types.
5. **Docs** as above.

## Result

Implemented and validated:

- Deleted `McpCallLog.cs` / `McpPayloadFormatter`; kept `McpActivitySources` for HostBroker catalog refresh only.
- Host SDK `CallToolFilters` / `ReadResourceFilters` + Daemon `invoke_dynamic` / `search_dynamic` log via `ILogger` + `McpJsonUtilities.DefaultOptions`.
- Soft clamp 4096; binary blocks log type/mime/length only.
- Tests: `DynamicToolsAndObservabilityTests` 6/6 passing.
- Visual: published Daemon stdio log shows `tools/call ok target=search_dynamic durationMs=... args={...} result={...}`.
- Docs: `docs/product/mcp.md`, `docs/architecture/MCP/tools.md`.

