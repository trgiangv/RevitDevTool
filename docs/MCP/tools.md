# MCP Tools and Runtime Catalog

## Stable daemon tools

Default `Broker` mode exposes exactly six daemon tools. Its list is deliberately stable as hosts and host catalogs change.

| Tool | Contract |
|---|---|
| `devtools_search` | Searches a cached snapshot. Optional `query`, `kinds`, daemon-local PID `hostId`, `detail`, and `limit` narrow results. |
| `devtools_invoke` | Invokes a `tool:<name>`, `resource:<uri>`, or `prompt:<name>` target. Optional `hostId` disambiguates a PID; `arguments` apply to tools/prompts. |
| `launch_host` | Uses a registered `IHostDriver` to launch a supported product. |
| `open_model` | Resolves a compatible host driver from the model extension and opens it. |
| `read_file_info` | Reads supported offline metadata through a host driver. |
| `list_machines` | Calls the authenticated gateway machine endpoint after connection selection. |

`devtools_search` uses only cached snapshots and returns target, kind, host metadata, and (at schema detail) tool input schema. `devtools_invoke` forwards exactly one standard MCP operation to the resolved host. If several hosts provide a target and no matching `hostId` is supplied, it returns an ambiguity result containing the local candidate PIDs.

`devtools_invoke.timeoutSeconds` is bounded from 1 through 900 seconds and defaults to 300. Its structured payload uses one of seven statuses:

| Status | Meaning |
|---|---|
| `host_selection_required` | The target exists on multiple hosts; choose one candidate `hostId`. |
| `host_mismatch` | The supplied `hostId` does not publish the target. |
| `target_not_found` | The current cached catalog has no matching target. |
| `host_disconnected` | The selected generation disconnected before dispatch. |
| `connection_lost` | The host connection failed while dispatching. |
| `timed_out` | The request exceeded its requested broker deadline. |
| `host_failed` | Argument conversion or the host operation failed for another reason. |

The payload includes `mayHaveExecuted`. It is false until a request could have been dispatched and true for connection-loss, timeout, and host-failure results after dispatch. The broker deliberately does not retry any invocation: an ambiguous execution outcome must be resolved by the caller.

Search also includes generation-scoped catalog states: `refreshing`, `ready`, `stale`, and `unavailable`. `ready` and `stale` entries are searchable; `refreshing` and `unavailable` report status without primitive entries.

The broker is a discovery and invocation surface, not a dynamic tool-list relay. Arbitrary primitives require search then invoke; known unique targets can be invoked directly.

## Native mode

The persisted daemon setting defaults to `Broker`. Opt-in `Native` mode retains the six daemon tools and adds namespaced SDK proxies for host tools, prompts, resources, and resource templates. It is suitable only for MCP clients that honor list-changed notifications. Do not make Native the compatibility default: runtime catalog changes must not invalidate clients that load `tools/list` once.

## Host catalog

Hosts publish standard SDK primitives. Built-ins include the shared execution tools (`execute_csharp_code`, `execute_python_code`, `open_document`), host-provided navigation, resources, and prompts, plus .NET/Python primitives loaded from configured paths. The exact available host catalog depends on installed host integrations and configured toolsets.

Names and URIs are identities:

- Built-in names are reserved.
- Dynamic duplicate names and URIs are rejected with diagnostics instead of shadowing another primitive.
- Registry settings preserve accepted .NET/Python paths and prune invalid paths.
- In `Broker` mode, the daemon snapshots host catalog entries by PID; in `Native` mode, it creates SDK proxies from those snapshots.

## Scope of `hostId`

`hostId` is an integer host PID local to one selected daemon. It is unrelated to
the gateway's `machine_id`. For gateway use, first call authenticated `GET
/machines`, select one `machine_id`, and send `x-target-machine` on MCP
initialization. The returned `mcp-session-id` retains that binding for later
HTTP requests. Only then can broker `hostId` choose a host process on that
machine.

`list_machines` cannot bootstrap an unpinned multi-machine gateway connection: the gateway needs `x-target-machine` before it forwards any MCP request.

There are no `dynamic_search` or `dynamic_invoke` aliases. Use the stable `devtools_search` and `devtools_invoke` names.
