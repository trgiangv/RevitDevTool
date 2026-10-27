# 0048 Update Engine Lives in `DevTools.Update`, Hosted by the Daemon

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Part of the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).

## Context

Something has to run on a schedule, outside any Revit or AutoCAD process, while
hosts of several years may be open or closed. Candidates on the machine today:

| Process | Lifetime | Fit |
|---------|----------|-----|
| Host add-in (`RevitDevTool`, `AcadDevTool`) | One per open host | Its own year folder is locked while it runs; many instances would race |
| `DevTools.Daemon.exe` tray | Per user, HKCU Run, single instance (mutex `DevToolsDaemon_v1`) | Already long-running, has tray, settings, logs, host discovery |
| `DevTools.Daemon.exe --stdio` | One per MCP client | Short-lived, many instances |
| Windows Task Scheduler task | OS-owned | No tray or UI; another thing to install and remove |

0018 makes the Daemon a composition root only: it registers services and does
not implement domain logic. The Daemon already references `DevTools.Hosting`,
`Hosting.Revit`, and `Hosting.Acad` (`IHostPathResolver.GetInstalledVersions`)
and discovers running hosts through `HostConnections` and `HostPipeName`.

GitHub limits unauthenticated REST calls to 60 per hour per IP
(`X-RateLimit-Limit: 60` on `/repos/.../releases/latest`). A 4-minute poll is 15
calls per hour per machine; an office behind one NAT IP runs out quickly.
`https://github.com/<repo>/releases/latest/download/<asset>` answers `302` with
`Location: .../releases/download/<tag>/<asset>` and no rate-limit headers.

## Decision

### Placement

- New project `DevTools.Update` (net10.0-windows, headless, no WPF). It owns
  the scheduler, the Velopack `IUpdateSource` (0053), year inventory, lock
  probe, planner, stager, and applier. It references the `Velopack` package;
  Velopack does the Daemon side, `DevTools.Update` the year side.
- It references `DevTools.Hosting` for `HostApp` and `IHostPathResolver`. It does
  not reference `Hosting.Revit` / `Hosting.Acad`; the Daemon passes the resolvers in.
- The Daemon calls `AddUpdateEngine()` and runs it as a hosted service **in tray
  mode only**. `--stdio` processes never check or apply updates.
- Hosts do not run update logic and do not know about updates.

### Inventory: what is installed vs what exists

The engine joins three sources each cycle:

| Source | Answers |
|--------|---------|
| `install-state.json` (0047) | Which host × year the user enabled, and the version of each year folder |
| `IHostPathResolver.GetInstalledVersions` | Which host years exist on the machine now |
| Bundle `Contents\<year>\` on disk | What is actually deployed (file versions) |

A host year that is newly installed on the machine and not selected is reported
once (toast, 0050). It is never added without the user.

### Release discovery and polling

- **Provider:** `IReleaseProvider` returns a `Release` (version and asset URLs).
  `GitHubReleaseProvider` uses Octokit: `GetLatest` for stable, the Releases list
  for prerelease. One REST call per check. A private deploy server is another
  provider. Velopack's `GithubSource` is not used, because the engine must verify
  the feed signature first (0051); `VelopackReleaseSource` adapts any provider.
- (Revised 2026-10-25: the first draft used `HEAD releases/latest/download/…` and a
  hand-parsed `Location` header. Typed Octokit models replace it.)
- **Interval:** user setting, default 60 minutes, floor 15 minutes, with ±20 %
  jitter. Also check once at Daemon start (after a 2-minute delay) and on demand
  from the tray. A 4-minute interval was rejected: on the REST API it spends 15
  of the 60 calls per hour that every machine behind the same IP shares, and on
  the download endpoint it gains nothing, because releases ship days apart.
- **Backoff:** on 403/429 or network failure, double the interval up to 24 hours;
  honor `Retry-After`. The primary limit waits for `x-ratelimit-reset`; a secondary
  limit waits for `Retry-After`, else at least 60 seconds.
- (Revised 2026-10-27) **Asset retry:** asset downloads (manifest, signatures,
  Velopack feed and packages, payload zips) are not REST calls and do not count
  against the 60/hour limit. A download that fails with a network error, 408, 429
  or 5xx starts again up to 3 times (2 s, 4 s) in the same check. A 4xx is final.
  REST calls are not retried in the check; the scheduler backoff covers them.
- (Revised 2026-10-27) **Request cache:** each provider answer is kept for 1 minute,
  so a check from the Updates tab (load, check, reload) sends 2 REST calls, not 5.
- Proxy: use the system proxy (`HttpClient` default) so corporate PAC/WPAD works.

### Running-instance awareness

The engine asks one question per year: is the year folder in use? It does not
ask which host uses it. It uses the lock probe from 0049 (Restart Manager), not
the MCP pipe list, because a host can run without MCP and the pipe list does not
cover every locker. `HostConnections` is used only for display names in toasts.

### Updater self-update

The engine ships inside the Velopack Daemon package, so it updates itself
through Velopack. Order, apply gate, and crash-on-start rollback are in
[0052](0052-updater-self-update.md).

## Alternatives Considered

1. **Logic inside the Daemon project.** Fastest, but breaks 0018 and cannot be
   unit-tested without the WPF app.
2. **Scheduled task + headless updater exe.** Works with the tray closed, but
   adds install/uninstall surface and a second notification path.
3. **Each host checks on startup.** No extra process, but the host's own year is
   locked at that moment, and N open hosts race on one bundle.
4. **Authenticated API calls.** Higher limit, but shipping a token is not acceptable.
5. **In-host check, RevitLookup style.** `SoftwareUpdateService` runs in Revit
   and reads the REST `releases` list. Fine for one host per year, but every open
   host repeats the call and none can replace its own locked folder.

## Consequences

Positive:

- One updater per user session, aligned with the existing single-instance tray.
- Stable checks cost no API quota, so short intervals are safe.
- Engine logic is testable in `tests/DevTools.Update.Tests` without a host.

Tradeoffs:

- No updates while the tray is not running (user quit it or disabled auto-start).
- The Daemon becomes a required component, not an MCP-only one (0047).
- Relies on the undocumented `302 Location` shape of `releases/latest/download`;
  the engine falls back to downloading the manifest when `Location` does not parse.
- A new release reaches a machine up to about an hour after publish.

## Follow-Up

- Create `DevTools.Update` and `tests/DevTools.Update.Tests` (MSTest.Sdk per 0035).
- Add update settings to `UserSettings` and the Daemon settings view.
- Update `docs/architecture/MCP/daemon.md` responsibilities list.
