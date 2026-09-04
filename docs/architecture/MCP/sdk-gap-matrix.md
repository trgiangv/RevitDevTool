# MCP C# SDK gap matrix

Living comparison between **ModelContextProtocol 2.2.0** (`2026-07-28` protocol family) and
the DevTools stack as of 2026-09-04.

**Packages:** `Directory.Packages.props` → `ModelContextProtocol` + `ModelContextProtocol.Extensions.Tasks` **2.2.0**.

**Host wire policy:** [0027](../../decisions/0027-mcp-product-surface.md) — SDK DTOs/constants
allowed on host; host named pipe does **not** run `McpServer` / `McpSession`.

**Product limits:** [0027](../../decisions/0027-mcp-product-surface.md) —
stabilize host↔daemon schema and errors; MRTR is not a product workflow.
Progress on host/ALC is unsupported. JSON facades: [0031](../../decisions/0031-daemon-json-source-gen.md).

**Not MRTR product:** wire-level MRTR plumbing lives in
[`platform-boundaries.md`](platform-boundaries.md). Historical G3/G4/G5 labels
are frozen in [`2026-08-02-mrtr-implementation.md`](../../plans/completed/2026-08-02-mrtr-implementation.md)
— not a delivery backlog ([0027](../../decisions/0027-mcp-product-surface.md)).

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Adopted and covered by tests or live checklist |
| ⚠️ | Partial / custom path / test gap |
| ⏸ | Intentionally deferred |
| ❌ | Not supported (by design or not yet) |

---

## Protocol & server capabilities

| SDK / spec capability | DevTools | Notes |
|-----------------------|----------|-------|
| Protocol `2026-07-28` negotiation | ✅ | Daemon SDK server; host spec wire (`server/discover`) |
| `tools/list` + `listChanged` | ✅ Host; daemon `ListChanged=false` | By design |
| `resources/list` + templates | ✅ | Host catalog; daemon via `search_dynamic` |
| `resources/subscribe` | ⏸ | `Subscribe=false` on host — noisy on live BIM |
| `prompts/list` / `prompts/get` | ✅ Daemon-only | Host prompts not registered |
| `completions` | ⏸ | Low ROI for opaque `capabilityId` flow |
| Progress notifications | ⚠️ Daemon fixed tools ✅; host pipe / `invoke_dynamic` / ALC / Python / built-ins ❌ | Daemon SDK `McpServer` can emit `notifications/progress`. Host path does not: `InvokeDynamicTool` omits `Meta` (including `progressToken`); ALC `ToolsetProgressReporter` calls `McpServer.NotifyProgressAsync` on a `RequestFactory` server whose `ToolExecutionTransport` swallows outbound messages. [0027](../../decisions/0027-mcp-product-surface.md) non-goal. |
| MCP Tasks extension | ✅ | `WithTasks`; `Optional` on export + execute tools; client `_meta` opt-in |

---

## `tools/call` — content blocks

| `ContentBlock` type | Host adapter | `invoke_dynamic` pass-through | Tests |
|---------------------|--------------|-------------------------------|-------|
| `TextContentBlock` | ✅ | ✅ | Harness + structured output |
| `ImageContentBlock` | ✅ | ✅ | `view_screenshot`, harness |
| `AudioContentBlock` | ✅ | ✅ | No product tool |
| `EmbeddedResourceBlock` | ✅ | ✅ | Harness |
| `ResourceLinkBlock` | ✅ round-trip | ✅ | Pass-through in mapper tests |
| `ToolUseContentBlock` | ❌ | ❌ | Sampling-only; not product surface |
| `ToolResultContentBlock` | ❌ | ❌ | Same |

---

## `tools/call` — `CallToolResult` fields

| Field | Daemon fixed tools | Host built-in | Host .NET toolset (ALC) | `invoke_dynamic` |
|-------|-------------------|---------------|-------------------------|------------------|
| `Content` | ✅ compact text mirror | ✅ SDK path | ✅ via `ReturnMapper` | ✅ pass-through |
| `StructuredContent` | ✅ manual on daemon envelope tools | ✅ wire DTO | ✅ via `ToolsetResultSerializer` | ✅ pass-through |
| `OutputSchema` on tool def | ⏸ daemon envelope tools (Cursor workaround) | ✅ wire list | ✅ parser metadata | via `detail=schema` search |
| `UseStructuredContent` | ⏸ daemon envelope tools | — | ✅ toolsets | — |
| `IsError` | ✅ | ✅ | ✅ | ✅ harness |
| `Meta` | ✅ | ✅ | ✅ | ✅ harness |

**ALC note:** Isolated toolsets do **not** use SDK `McpServerTool.InvokeAsync` result switching.
See [`platform-boundaries.md`](platform-boundaries.md).

---

## MRTR (`InputRequiredResult`)

| Layer | Status | Notes |
|-------|--------|-------|
| SDK types on wire | ✅ | `InputRequiredResult`, `InputRequiredException` |
| Daemon → host single round-trip | ✅ | `CallToolPassthroughAsync` (≈ python `allow_input_required`) |
| Daemon → external client forward | ✅ | `InvokeDynamicTool` + `InvokeDynamicMrtrState` |
| Mock tests (daemon hop) | ✅ | `InvokeDynamicSdkHarnessTests` T-D-* |
| ALC create-time `IsAugmentedWith` bind | ✅ | Local mirror of SDK four augmented types |
| ALC low-level throw/retry via `Params` | ✅ | T-ALC-10..15 unit/harness |
| ALC high-level `ElicitAsync` / `MrtrContext` | ❌ | Sync `InvokeSync`; documented unsupported — [`platform-boundaries.md`](platform-boundaries.md) G1-c |
| Python toolset MRTR (payload + `InputRequiredResult`) | ✅ | Normalizer + parser + unit tests; live Python.NET elicitation E2E ⏸ ([0027](../../decisions/0027-mcp-product-surface.md)) |
| Python `Resolve(Elicit[T])` in toolset | ❌ | Embedded bridge — not python-sdk Resolve graph |
| Product destructive confirm | ✅ | Warning + `dryRun`; elicitation is **not** the product path ([0027](../../decisions/0027-mcp-product-surface.md)) |
| Host legacy / `IsMrtrSupported` | ⏸ | Not scheduled ([0027](../../decisions/0027-mcp-product-surface.md)) |
| Gateway E2E elicitation | ⏸ | Not scheduled ([0027](../../decisions/0027-mcp-product-surface.md)) |

Detail + test matrix: [`2026-08-02-mrtr-implementation.md`](../../plans/completed/2026-08-02-mrtr-implementation.md).

---

## Resources

| Feature | Status | Notes |
|---------|--------|-------|
| Fixed URI resources | ✅ | Built-in cheatsheets, model context |
| Resource templates | ✅ | `revit://element/{elementId}`, schedule preview |
| Template read via `invoke_dynamic` | ✅ | `arguments` + batch `reads[]` |
| `UriTemplate` from catalog metadata | ✅ | `DotnetMcpCatalogCreateOptions` |
| Resource `listChanged` | ✅ | Host broker refresh |

---

## DevTools custom patterns (not SDK defaults)

| Pattern | Why |
|---------|-----|
| Opaque `capabilityId` | Daemon-local locator; external surface stays two tools |
| `CallToolPassthroughAsync` | Avoid client auto-MRTR on daemon→host hop |
| `ToolsetInvoker` + `ToolsetResultSerializer` | ALC toolset invoke + JSON bridge to host wire DTOs |
| `ToolsetInvocationServices` | Mirror SDK internal request DI without `InternalsVisibleTo` |
| `InvokeDynamicMrtrState` | Embed `capabilityId` in daemon `requestState` (protocol field, not `__mcp*`) |
| Toolset MCP `ExcludeAssets=runtime` | Toolset-only | Compile against MCP; no MCP DLLs in toolset output; `McpToolsetContext` + `AssemblyResolve` maps to host MCP. |

---

## Test & contract gaps (SDK alignment)

| Gap | Priority | Tracking |
|-----|----------|----------|
| `ContractTests` — `outputSchema` on catalog `tools/list` for structured toolsets | Low | Optional; daemon envelope tools stay without inferred `outputSchema` ([0027](../../decisions/0027-mcp-product-surface.md)) |
| Embedded resource block — dedicated contract beyond harness | Low | Covered in harness |
| Live Gateway MRTR | ⏸ | [0027](../../decisions/0027-mcp-product-surface.md) — not product |
| ALC `IsAugmentedWith` + MRTR round-trip tests | Done | T-ALC-* green |
| Host stateful backcompat vs daemon passthrough | ⏸ | [0027](../../decisions/0027-mcp-product-surface.md) — not product |
| Python toolset live MRTR E2E (Python.NET) | ⏸ | [0027](../../decisions/0027-mcp-product-surface.md) |
| Python `Resolve(Elicit[T])` in toolset | Low | Explicit non-goal |
| Host / ALC progress notifications | ⏸ | [0027](../../decisions/0027-mcp-product-surface.md) |

**Verification:**

```powershell
dotnet run --project tests/DevTools.Mcp.Server.Tests/DevTools.Mcp.Server.Tests.csproj -- --filter "InvokeDynamicSdkHarness|StructuredOutput"
dotnet run --project tests/DevTools.Mcp.Catalog.Tests/DevTools.Mcp.Catalog.Tests.csproj -- --filter "ToolsetResultSerializer|RevitMcpToolSetParser"
```

---

## Deferred SDK features (documented, not gaps)

These are **product choices**, not incomplete adoption:

- `resources/subscribe`
- `completions`
- `ToolUse` / `ToolResult` content blocks on tools
- Native audio product tools
- MRTR elicitation for bulk delete (warning-first policy)
- Host / ALC progress notifications
- Gateway / host-legacy MRTR elicitation — [0027](../../decisions/0027-mcp-product-surface.md)

---

## Related

| Doc | Role |
|-----|------|
| [`platform-boundaries.md`](platform-boundaries.md) | ALC + layer map + MRTR wire detail |
| [`tools.md`](tools.md) | Daemon/host tool inventory |
| [`product/mcp.md`](../../product/mcp.md) | External behavior contract |
| [0027 MCP product surface](../../decisions/0027-mcp-product-surface.md) | Daemon envelope; not full protocol |
| [0012 Host MCP spec engine](../../decisions/0012-host-mcp-spec-engine.md) | Host pipe; partially superseded by 0027 |
| [0031 Daemon JSON source-gen](../../decisions/0031-daemon-json-source-gen.md) | Source-gen JSON for 0032 AOT |
| [`2026-08-02-mcp-advanced-features-adoption.md`](../../plans/completed/2026-08-02-mcp-advanced-features-adoption.md) | Feature adoption session |
| [`2026-08-02-mrtr-implementation.md`](../../plans/completed/2026-08-02-mrtr-implementation.md) | Historical G1 done; elicitation/progress closed by 0027 |
