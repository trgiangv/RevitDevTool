# MCP Product Contract

External AI clients reach host capabilities through `DevTools.Daemon`.
In-host MCP runtime is shared across registered hosts.

## Behavior

- Daemon owns stdio MCP, gateway, auth, host discovery, and a **fixed** external
  tool/prompt surface (`ListChanged = false`).
- Infrastructure tools remain on the daemon (`list_host_instances`, `launch_host`,
  `read_file_info`, `list_machines`, …).
- Host capabilities are **not** projected into daemon `tools/list`. Clients use
  exactly two dynamic operations:
  - `search_dynamic(query, hostInstanceId?, kinds?, limit?, detail?)` — in-memory
    search of `ConnectedHostCatalog` (tools, resources, resource templates). `limit` is
    validated as 1–32 (default 12), and `detail` is `summary` (default) or
    `schema`. Search normalizes whitespace, `_`, and `-`, ranks all-token matches
    before partial matches, and returns compact bounded items plus `hasMore`.
    Every item has an opaque, catalog-versioned local `capabilityId`, kind, target,
    host routing, short description, `requiredArgs`, and `argsHint`; only
    `detail=schema` includes a tool input schema. A capability ID is a daemon-local
    route locator, not a secret or durable/global capability identity.
  - `invoke_dynamic(capabilityId, arguments?)` — routes one capability ID through
    the current SDK `McpClient` session and returns the host-native MCP payload:
    `CallToolResult` for tools (including `image` / `audio` content blocks) and
    `EmbeddedResourceBlock` entries for single resource/template reads. Validation
    and stale-locator failures still return compact JSON in a text content block.
    Alternatively, `invoke_dynamic(reads: [{ capabilityId, arguments? }])` performs
    read-only batch resource/template reads. Single-target fields and `reads` are
    mutually exclusive; tools are rejected in batches. `reads` defaults to at most
    16 items and has a hard 64-item validation limit. Batch output has a 1 MiB
    UTF-8 serialized-result budget (4 MiB hard ceiling): complete items are
    appended only, and one oversized item is represented by a typed per-item error.

    Catalog replacement, host disconnection, capability removal, or capability
    change makes a locator stale with reason `host_disconnected`,
    `host_catalog_changed`, `capability_removed`, or `capability_changed`.
    Stale errors are retryable only while `executionStarted=false`; clients retry
    once by `research_then_reinvoke` (search again, then invoke the new ID). No
    separate resolve tool exists.
- **MRTR is not a product workflow** ([0027](../decisions/0027-mcp-product-surface.md)).
  The working loop is search → invoke → read result or execute error tags
  (`[COMPILATION ERROR]`, `[RUNTIME ERROR]`, `[ROLLBACK]`), then retry. Destructive
  tools use structured **warning** + `dryRun` (e.g. `revit_delete_elements`), not
  elicitation. The host hop may still serialize `InputRequiredResult` if a tool
  throws `InputRequiredException` (plumbing); do not build agent features on
  Gateway/Cursor elicitation, `ElicitAsync`, or Python `Resolve(Elicit)`.
  Isolated .NET toolsets: low-level throw/retry only; high-level `MrtrContext`
  suspend is unsupported on the sync ALC invoker.
- `read_file_info` defaults to `detail=summary` for on-disk file peek; pass
  `detail=full` for complete transmission/link metadata. Success responses include
  SDK `StructuredContent` plus compact JSON in `Content` (prefer `StructuredContent`
  for machine parsing).
- `search_dynamic` and `list_host_instances` emit `StructuredContent` on success
  (manual path — no `OutputSchema` on `tools/list` until clients accept inferred schemas).
- Agent-facing JSON from `search_dynamic`, `read_file_info`, `invoke_dynamic`
  errors/stale responses, and `invoke_dynamic` batch `reads` uses compact SDK
  `McpJsonUtilities` (not indented pretty-print).
- Fixed prompts (`revit_code`, `acad_code`) are daemon-owned via native
  `prompts/list` / `prompts/get` and never contact a host.
- Two named-pipe protocols stay separate:
  - Pytest/control: `DevTools_{Host}_{Version}_{PID}` (`BridgeMessage`
    length-prefixed frames)
  - MCP: `DevToolsMcp_{Host}_{Version}_{PID}` (newline-delimited JSON-RPC)
- Host spec wire handler advertises list-changed so `HostBroker` can refresh only that
  host’s `ConnectedHostCatalog` entry; the external daemon collections stay unchanged.
- Call observability is always-on at protocol boundaries. Host in-process MCP logs
  via `McpLogFilters` and `ILogger`; daemon `search_dynamic` / `invoke_dynamic`
  log the same shape. Arguments and results are protocol JSON via SDK
  `McpJsonUtilities` (not hash-only summaries). Binary blocks are described by
  type / mime / length — never base64 `Data` on monitor lines.
- MCP Tasks extension (`io.modelcontextprotocol/tasks`) is advertised on the daemon SDK
  server. Clients opt in per request via `_meta`; `execute_csharp_code` and
  `execute_python_code` are **Optional** task-capable on the host. Infrastructure and
  dynamic daemon tools (`search_dynamic`, `invoke_dynamic`, …) stay synchronous.
- `view_screenshot` captures at **1280 px** width (Revit **150 DPI** unchanged;
  AutoCAD 1280×720). Use single `invoke_dynamic` for vision — batch `reads[]` stays JSON-only.

## Related

- Architecture: [`docs/architecture/MCP/README.md`](../architecture/MCP/README.md)
- SDK gaps: [`docs/architecture/MCP/sdk-gap-matrix.md`](../architecture/MCP/sdk-gap-matrix.md)
- Product surface: [`docs/decisions/0027-mcp-product-surface.md`](../decisions/0027-mcp-product-surface.md)
- Host pipe (partially superseded): [`docs/decisions/0012-host-mcp-spec-engine.md`](../decisions/0012-host-mcp-spec-engine.md)
- Boundaries (host wire): [`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
- Workflows: [`docs/architecture/MCP/workflows.md`](../architecture/MCP/workflows.md)
- Agent digest: [`docs/agents/mcp-pytest-bridge.md`](../agents/mcp-pytest-bridge.md)
