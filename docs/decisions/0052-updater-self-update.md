# 0052 Updater Self-Update Through Velopack, Plus Crash-on-Start Rollback

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Part of the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).
Built on [0053](0053-velopack-for-daemon-distribution.md). Replaces the
shared-payload rules that were in 0048 and 0049. An earlier draft of this ADR
had a custom swap and health handshake; Velopack now does the swap.

## Context

The update engine (`DevTools.Update`, 0048) is part of the Daemon package that
Velopack installs (0053). Velopack `apply` already:

- swaps `current\` by rename and restores it if the swap fails;
- ends processes whose image is under the Velopack root first;
- restarts the app after `ApplyUpdatesAndRestart`.

It does not detect a new version that installs fine but crashes at start. Then
the machine has no tray and no updater. It also ends Daemon `--stdio` processes
that MCP clients started, which breaks the user's AI session.

## Decision

### Order: updater first

Each cycle, the engine checks the Velopack feed before the year manifest. When
a Daemon update exists, it is downloaded first. Year updates still apply with
the current engine unless the year manifest's `minUpdaterVersion` (0046) is
newer than the running Daemon; then years wait for the self-update.

### The bootstrap contract is the Velopack feed

The Velopack feed (`releases.<channel>.json`) and its signature (0051) are the
only things an old engine must read to update itself. The year manifest may
change its schema freely; an engine that does not understand it updates itself
through Velopack first. The custom `IUpdateSource` must keep reading the feed
the same way in every release.

### Apply gate

- `auto` mode applies a Daemon update only when no `DevTools.Daemon.exe --stdio`
  process runs from `current\`. Otherwise the update is pending and the toast
  names the MCP clients to restart (0050).
- After 24 h pending, the toast offers **Update now**, which accepts that
  Velopack ends those processes.
- `notify` mode always asks.

### Crash-on-start rollback

- `Main` calls `VelopackApp.Build().Run()` first, then a start guard, before
  WPF, MCP, or the update engine load.
- The start guard keeps a counter in `%LOCALAPPDATA%\RevitDevTool\start-guard.json`.
  It increments the counter at start and resets it once the control pipe listens
  and the engine is loaded.
- Two failed starts in a row on a new version: the guard marks that version
  **bad**, then uses `UpdateManager` with `AllowVersionDowngrade = true` and a
  source pinned to `releases/download/<previous tag>/` to reinstall the previous
  version, and restarts.
- A version marked bad is skipped until a newer one is published. The restored
  Daemon shows the "Failed" toast.

### Setup.exe is not self-updated

Velopack `Setup.exe` is a one-shot installer. After install, the Daemon owns all
updates, including its own.

## Alternatives Considered

1. **Custom swap with a health handshake (earlier draft).** Keeps the old
   process alive to roll back instantly, but duplicates Velopack's swap and
   cannot work while Velopack owns `current\`.
2. **Separate updater exe (Squirrel `Update.exe` style).** Velopack already ships
   `Update.exe`; a second one adds nothing.
3. **No crash guard.** Simplest, but one bad release leaves every `auto` machine
   without an updater.
4. **Apply regardless of `--stdio` processes.** Fastest rollout, but silently
   kills MCP sessions.

## Consequences

Positive:

- Swap, delta, restart, and shortcut refresh come from Velopack.
- A Daemon that crashes at start rolls back without the user.

Tradeoffs:

- Rollback needs network to download the previous package.
- The start guard runs before the rest of `Main` and must stay small.
- Daemon updates can wait on long-lived MCP sessions.

## Follow-Up

- Start guard and downgrade path in the Daemon `Main`.
- Tests: crash on start twice, hang on start, bad-version skip, pending while
  `--stdio` runs.
