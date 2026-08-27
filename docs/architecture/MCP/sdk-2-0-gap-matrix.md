# MCP C# SDK 2.0.0 gap matrix

Living comparison between **ModelContextProtocol 2.0.0** (`2026-07-28` protocol family) and
the DevTools stack as of 2026-08-02.

**Packages:** `Directory.Packages.props` → `ModelContextProtocol` + `ModelContextProtocol.Extensions.Tasks` **2.0.0**.

**Not MRTR product:** wire-level MRTR gaps and product decisions live in
[`platform-boundaries.md`](platform-boundaries.md) and [`2026-08-02-mrtr-implementation.md`](../../plans/active/2026-08-02-mrtr-implementation.md).

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
| Progress notifications | ✅ | `IProgress<ProgressNotificationValue>` via `CallToolRequestServiceProvider` |
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
| Python toolset MRTR (payload + `InputRequiredResult`) | ✅ | Normalizer + parser + unit tests; live Python.NET E2E still open |
| Python `Resolve(Elicit[T])` in toolset | ❌ | Embedded bridge — not FastMCP Resolve graph |
| Product destructive confirm | ✅ (G2=B) | Warning + `dryRun`; MRTR elicitation deferred pending G3/G4 |
| Host legacy / `IsMrtrSupported` | ❓ | **G4** — stateful backcompat vs passthrough |
| Gateway E2E elicitation | ❓ | **G3** — not validated |

Detail + test matrix: [`2026-08-02-mrtr-implementation.md`](../../plans/active/2026-08-02-mrtr-implementation.md).

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
| `CallToolRequestServiceProvider` | Mirror SDK internal request DI without `InternalsVisibleTo` |
| `InvokeDynamicMrtrState` | Embed `capabilityId` in daemon `requestState` (protocol field, not `__mcp*`) |
| Toolset MCP `ExcludeAssets=runtime` | Toolset-only | Compile against MCP; no MCP DLLs in toolset output; `McpToolsetContext` + `AssemblyResolve` maps to host MCP. |

---

## Test & contract gaps (SDK alignment)

| Gap | Priority | Tracking |
|-----|----------|----------|
| `ContractTests` — `outputSchema` on catalog `tools/list` for structured toolsets | Low | Adoption plan optional item |
| Embedded resource block — dedicated contract beyond harness | Low | Covered in harness |
| Live Gateway MRTR | Medium | MRTR plan G3 / T-GW-* |
| ALC `IsAugmentedWith` + MRTR round-trip tests | Done | T-ALC-* green; live T-HOST-02 optional |
| Host stateful backcompat vs daemon passthrough | Medium | MRTR plan G4 / T-HOST-* |
| Python toolset live MRTR E2E (Python.NET) | Low | Bridge unit-tested; host spike optional |
| Python `Resolve(Elicit[T])` in toolset | Low | Explicit non-goal |

**Verification:**

```powershell
dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -- --filter "InvokeDynamicSdkHarness|StructuredOutput|ToolsetResultSerializer|RevitMcpToolSetParser|ContractTests"
```

---

## Deferred SDK features (documented, not gaps)

These are **product choices**, not incomplete adoption:

- `resources/subscribe`
- `completions`
- `ToolUse` / `ToolResult` content blocks on tools
- Native audio product tools
- MRTR elicitation for bulk delete (warning-first policy)

---

## Related

| Doc | Role |
|-----|------|
| [`platform-boundaries.md`](platform-boundaries.md) | ALC + layer map + MRTR wire detail |
| [`tools.md`](tools.md) | Daemon/host tool inventory |
| [`product/mcp.md`](../../product/mcp.md) | External behavior contract |
| [`2026-08-02-mcp-advanced-features-adoption.md`](../../plans/active/2026-08-02-mcp-advanced-features-adoption.md) | Feature adoption session (closing) |
| [`2026-08-02-mrtr-implementation.md`](../../plans/active/2026-08-02-mrtr-implementation.md) | MRTR G1 done; G2=B; G3/G4 open |
