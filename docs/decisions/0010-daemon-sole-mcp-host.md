# 0010 Daemon Is Sole MCP Host

Date: 2026-06-18

## Status

Accepted

## Context

A separate `DevTools.McpServer` process duplicated MCP hosting with the daemon.

## Decision

- `source/DevTools.McpServer/` is removed.
- `DevTools.Daemon` is the single MCP entry point for external AI clients
  (`--stdio` or gateway).
- Installer and publish pipeline pack/register only `DevTools.Daemon.exe`.

## Consequences

Positive: one process to configure, ship, and debug.

Tradeoffs: no backward compatibility for `MCPServer.exe`.
