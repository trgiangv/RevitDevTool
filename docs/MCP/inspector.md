# MCP Inspector attach (named pipe hosts)

Host MCP servers speak **standard MCP over a Windows named pipe**
(`DevTools_{Host}_{Version}_{PID}`), not stdio or SSE. MCP Inspector's default
stdio transport therefore cannot attach directly to an in-host pipe.

## Recommended attach path

1. Run **DevTools.Daemon** with `--stdio` (broker surface).
2. Point MCP Inspector at the daemon stdio process.
3. Use broker tools (`devtools_search` / `devtools_invoke`) to reach host
   primitives, or enable experimental Native mode only for clients that honor
   list-changed (see [tools.md](tools.md)).

Gateway Streamable HTTP is another Inspector-compatible path when a daemon is
tunneled and authenticated.

## Why a stdio↔pipe shim is deferred

A small stdio↔named-pipe proxy would let Inspector attach to one host PID
without the daemon, but it duplicates accept/ACL/legacy-frame behavior already
owned by `DevTools.Mcp.Hosting` and the daemon. Cost/benefit for the current
envelope (&lt;20 hosts) favors documenting the daemon/gateway attach paths above
instead of shipping a second transport shim.
