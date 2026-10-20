# MCP Product Contract

External AI clients reach host capabilities through `DevTools.Daemon`.
In-host MCP runtime is shared across registered hosts.

## Behavior

- Daemon owns stdio MCP, gateway, auth, host discovery, and a **fixed** external
  tool/prompt surface (`ListChanged = false`).
- Infrastructure tools remain on the daemon (`list_processes`, `launch_host`,
  `read_file_info`). `list_machines` is answered by the gateway.
- Host capabilities are **not** projected into daemon `tools/list`. Clients
  use `code_mode(code, readOnly?)`, whose `code` is a C# async method body.
  Search and dispatch run inside that program:
  - `SearchAsync` returns ranked candidates from the in-memory catalog. Treat
    them as options, not an instruction to invoke the first result. Inspect the
    description and call `DescribeAsync` for the schema when intent or arguments
    are ambiguous. Search is schema-aware lexical retrieval, not a guarantee of
    semantic equivalence. The detailed policy is in
    [decision 0040](../decisions/0040-bm25f-search-and-hit-annotations.md).
  - Specialized capabilities are preferred by default. C# and Python execution
    tools are the fallback when no specialized candidate is sufficiently
    relevant; explicit code-execution queries may surface them directly.
  - `DescribeAsync` returns the SDK `Tool`; `InvokeAsync` returns the host
    `CallToolResult` (including image and audio blocks); `ReadAsync` returns
    `ReadResourceResult`. The model receives only the program's return value.
    `readOnly` defaults to false; when true, `InvokeAsync` throws unless the
    tool's `ReadOnlyHint` is true.
  - Text over 40,000 characters keeps its start and end. The full text is saved
    under `%TEMP%`, and the return includes that path. Image, audio, and blob
    payloads over 1 MiB are rejected. Ordinary objects and arrays are returned
    as JSON text; `code_mode` does not advertise an `outputSchema`. A returned
    `CallToolResult` passes through when its text is within the limit.
- `execute_csharp_code` and `execute_python_code` descriptions include the
  required entry pattern. From `code_mode`, source is passed as one string and
  quotes inside that string must be doubled. Search and invocation locators
  remain inside the Daemon; neither `search_dynamic` nor `invoke_dynamic` is
  an external MCP tool.
- **MRTR is not a product workflow** ([0027](../decisions/0027-mcp-product-surface.md)).
  The working loop is one `code_mode` program: search, call, and return a projection.
  Execute error tags (`[COMPILATION ERROR]`, `[RUNTIME ERROR]`, `[ROLLBACK]`) are retried inside that program. Destructive
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
- `list_processes` emits `StructuredContent` on success
  (manual path — no `OutputSchema` on `tools/list` until clients accept inferred schemas).
- Agent-facing JSON from `code_mode` results, `read_file_info`, and tool errors
  uses compact SDK `McpJsonUtilities` (not indented pretty-print).
- Fixed prompts (`revit_code`, `acad_code`) are daemon-owned via native
  `prompts/list` / `prompts/get` and never contact a host.
- Two named-pipe protocols stay separate:
  - Pytest/control: `DevTools_{Host}_{Version}_{PID}` (`BridgeMessage`
    length-prefixed frames)
  - MCP: `DevToolsMcp_{Host}_{Version}_{PID}` (newline-delimited JSON-RPC)
- Host SDK server advertises `listChanged` so `HostConnections` can refresh only that
  process catalog; the external daemon collections stay unchanged.
- Call observability is always-on at protocol boundaries. Host in-process MCP logs
  via `McpLogFilters` and `ILogger`; daemon `code_mode` and infrastructure tools
  log the same shape. Arguments and results are protocol JSON via SDK
  `McpJsonUtilities` (not hash-only summaries). Binary blocks are described by
  type / mime / length — never base64 `Data` on monitor lines.
- MCP Tasks extension (`io.modelcontextprotocol/tasks`) is advertised on the daemon SDK
  server. `TaskSelection` returns **Synchronous** or **Optional** only. `launch_host`
  and `code_mode` are **Optional**. The other infrastructure tools stay synchronous. **Required**
  is unused until clients advertise the tasks extension; it rejects the call before
  the tool runs. Daemon-to-host `tools/call` stays synchronous; task polling is
  client ↔ daemon only.
- `view_screenshot` captures at **1280 px** width (Revit **150 DPI** unchanged;
  AutoCAD 1280×720). Return the `ImageContentBlock` from `code_mode` when the model must see it.

## Related

- Search behavior: [decision 0040](../decisions/0040-bm25f-search-and-hit-annotations.md)
- Proposed semantic-retrieval evaluation: [decision 0044](../decisions/0044-hybrid-tool-retrieval.md)
- Architecture: [`docs/architecture/MCP/README.md`](../architecture/MCP/README.md)
- SDK gaps: [`docs/architecture/MCP/sdk-gap-matrix.md`](../architecture/MCP/sdk-gap-matrix.md)
- Product surface: [`docs/decisions/0027-mcp-product-surface.md`](../decisions/0027-mcp-product-surface.md)
- Host pipe (partially superseded): [`docs/decisions/0012-host-mcp-spec-engine.md`](../decisions/0012-host-mcp-spec-engine.md)
- Boundaries (host wire): [`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
- Workflows: [`docs/architecture/MCP/workflows.md`](../architecture/MCP/workflows.md)
- Agent digest: [`docs/agents/mcp-pytest-bridge.md`](../agents/mcp-pytest-bridge.md)
