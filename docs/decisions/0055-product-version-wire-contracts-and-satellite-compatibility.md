# 0055 Product Version, Wire Contracts, and Satellite Compatibility

Date: 2026-10-29

## Status

**Proposed 2026-10-29.** Builds on the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0054](0054-release-automation-channels-and-4-0-cutover.md)),
[0043](0043-ipc-shared-infrastructure-separate-protocols.md) (the pipes), and
[0009](0009-multi-host-pytest-client.md) (pytest client). It does not change
what the update engine installs. It changes how the pieces that talk to each
other prove that they can.

## Context

A release stamps one product version (`4.0.0-beta.4`) on the Daemon, TestRunner,
and every per-year payload (`ResolveVersionModule` → `VersionPrefix` /
`VersionSuffix`). The update engine compares those versions as SemVer
(`Versions.IsNewer`) and gates payloads on `minUpdaterVersion` (0046, 0052).

Four things talk to that set and are **not** updated by it:

| Satellite | Where | Version today | Updated by |
|-----------|-------|---------------|------------|
| `RevitDevTool.TestAdapter` (NuGet) | this repo, `source/DevTools.TestAdapter` | `<Version>0.1.4</Version>`, bumped by hand, "independent of installer GitVersion" | The consumer, in their csproj |
| `revitdevtool_pytest` (PyPI) | `RevitDevTool.PyTest` repo | `__version__ = "0.5.0"` | The consumer, `uv`/`pip` |
| `McpGateway` (Cloudflare Worker) | `McpGateway` repo | none | Deploy by the owner |
| MCP clients (Claude, Cursor, VS Code) | outside | the MCP spec date | The client vendor |

### Wires, as read from code (2026-10-29)

| Wire | Between | Identifier | Check today |
|------|---------|------------|-------------|
| Testing, leg 1 (stdin/stdout NDJSON) | Adapter → `DevTools.TestRunner.exe` | `TestRunExecute.ProtocolVersion` (int) | `ProcessTestRunnerClient` rejects a request whose `ProtocolVersion != CurrentVersion` |
| Testing, leg 2 (test pipe, `BridgeMessage`) | Runner → host add-in | `testing/hello` → `TestingProtocol.CurrentVersion = 2` | `IsCompatible` is `==`. Checked in `TestPipeClient`, `ProcessTestRunnerClient`, `TestingRequestHandler` |
| Pytest (test pipe) | `revitdevtool_pytest` → host add-in | none. Methods `tests/run`, `ipytests/run`, `notifications/tests/progress` | None. `models.py` is synced by hand with `PytestContracts.cs` |
| Instance discovery (test pipe) | any client → host add-in | `instance/info` → `InstanceInfo { HostApp, ProcessId, VersionNumber }` | `VersionNumber` is the **host application** version (`2025`), not the add-in version |
| MCP, Daemon → host pipe | Daemon → host `McpServer` | `McpSpecKeys.ProtocolVersions.Current = "2026-07-28"`, per-request `_meta` | `McpProtocol.IsCurrent` (ordinal `==`); `ProcessSession` also compares `>=` |
| Tunnel | Daemon → `McpGateway` WebSocket | `register` / `heartbeat` messages | None. The gateway forwards `MCP-Protocol-Version` and the JSON-RPC body |

The product version is read in three different ways, and two lose the
prerelease suffix: `ShellViewModel` uses `GetName().Version.ToString(3)`
(`4.0.0` for `4.0.0-beta.4`); `SentryTelemetry` uses the 4-part assembly
version. The update engine uses Velopack for the Daemon and its own store
(`install-state.json`) for payloads, never the assembly.

### What goes wrong

1. **The only gate is exact equality on one integer.** A bump is always
   breaking, so a bump is avoided or done with no path for old peers.
2. **The gate checks the number, not the schema.** An adapter and a host that
   both say `2` but disagree on a record shape fail late, as a JSON error.
3. **A lag window exists inside one release.** The Daemon and TestRunner swap
   with Velopack on exit. A payload can stay `PendingVersion` while Revit holds
   its folder (0049). If a release changes the wire, the new runner meets the
   old host until the user closes Revit. `minUpdaterVersion` only orders the
   other direction (payload waits for Daemon).
4. **Errors do not say what to do.** `Testing protocol version 3 is not
   supported. Expected 2.` does not say whether to update the adapter, the
   product, or pytest, and neither side reports the add-in's product version.
5. **The satellite release is manual and unrelated to the product release.**
   Nothing fails if a release changes the wire and the nupkg is not
   republished, or the reverse.
6. **Pytest and the gateway have no gate at all.**

## Decision

### 1. Three separate notions; each has one job

| Notion | Form | Used for | Never used for |
|--------|------|----------|----------------|
| **Product version** | SemVer, one per release, stamped on every binary this repo ships (not the nupkg, §5) | Display, support, update ordering (0046) | Deciding whether two peers can talk |
| **Contract range** | Per wire: `Current` and `Min` (integers) | The only compatibility decision | Display |
| **Spec identifier** | The MCP date string | Spec negotiation with MCP clients (SDK) | Anything in this ADR |

Compatibility is decided by contract ranges, not by comparing product
versions. This avoids SemVer prerelease ordering rules (`beta.4 < beta.10`,
`4.0.0-beta.9 < 4.0.0`) in a place where they would be easy to get wrong.

### 2. Range negotiation replaces exact equality

Each endpoint of a wire declares `Current` and `Min` with `Min ≤ Current`.

```
compatible(A, B)  = A.Min <= B.Current  and  B.Min <= A.Current
negotiated(A, B)  = min(A.Current, B.Current)
```

Both ends speak `negotiated` for the whole session. If not compatible, the
side whose `Current` is below the other's `Min` is **too old**; that side is
the one to update (§4).

**Legacy peers.** A peer that sends only `ProtocolVersion = p` is read as
`Current = Min = p`. The responder answers `ProtocolVersion = negotiated`,
which is `p` for such a peer. The shipped `RevitDevTool.TestAdapter 0.1.x`
checks `hello.ProtocolVersion == 2` and so keeps working while the host's
range includes 2.

### 3. When `Current` and `Min` change

| Change | `Current` | `Min` |
|--------|-----------|-------|
| New optional field, or a new method a peer can skip | no change | no change |
| New behavior that a peer must opt in to | `+1` | no change |
| Removal, rename, or a changed meaning | `+1` | raised to the new `Current`, **only in a new major product version** |

Rules that follow:

- **Within a stable major (`4.x.y`), `Min` is fixed.** Any stable adapter,
  runner, host, and pytest of one major interoperate. This is also what makes a
  Velopack rollback inside a major (0052) safe.
- **Prerelease series are exempt.** Between `4.0.0-beta.N` builds, `Min` may
  rise. The range check still runs, so a beta mismatch fails with the §4
  message, not a JSON error.
- **`4.0.0` keeps `Min = 2` on the testing wire.** Existing `0.1.x` adapters
  keep working. Raising `Min` is a deliberate act of a later major, because a
  `0.1.x` adapter prints a message that cannot explain it.
- **Unknown members are ignored and defaults are explicit** on every
  `BridgeMessage` payload and `TestRunExecute`/`TestRunnerStreamMessage`. This
  is what makes "new optional field" safe. Golden tests (below) enforce it.

### 4. Handshake carries versions and produces a remedy

The testing `hello` (both legs) and `instance/info` carry, in addition to the
range:

| Field | Meaning |
|-------|---------|
| `ProductVersion` | Informational version of the **add-in** (host side) or runner, with prerelease suffix, without build metadata |
| `Protocols` | Map of wire name → `{ current, min }` for every wire that endpoint serves (`testing`, `pytest`) |
| `ClientKind` / `ClientVersion` | `adapter`, `pytest`, `runner` plus the package version of the caller |

`ProtocolIncompatible` details carry both ranges, both product versions, and a
`remedy` computed with §2:

| Condition | `remedy` | Message (shape) |
|-----------|----------|-----------------|
| client `Current < ` host `Min` | `update-client` | `RevitDevTool.TestAdapter 4.0.0 is too old for RevitDevTool 5.0.0. Update the package.` |
| host `Current < ` client `Min` | `update-product` | `RevitDevTool.TestAdapter 5.0.0 needs RevitDevTool ≥ protocol 4; installed 4.0.0 speaks 3. Update in the Daemon Updates tab.` |
| host reports `PendingVersion` payload | `restart-host` | `The RevitDevTool update is installed when Revit closes.` |

`ProductVersion` is displayed in the message and logged. It is not compared.

### 5. The NuGet package has its own version and is published only when it changes

A product release often changes neither the adapter nor the testing wire.
`RevitDevTool.TestAdapter` therefore keeps its **own SemVer**, and a release
publishes it only if its content changed. Compatibility with the product is
the range check (§2), not the package number.

- **What counts as a change.** The packed content: the adapter and its private
  runtime closure, the framework MTP assemblies, the `build/` targets, and the
  embedded `DevTools.Testing.Transport`, `Testing.Abstractions`, and `Ipc`
  assemblies. A change to `Current` or `Min` of a wire the adapter speaks is a
  change by definition.
- **Release step.** `PublishRelease.yml` packs the adapter and compares a
  content hash (over the files, not the nupkg bytes, which carry timestamps)
  with the latest version on nuget.org.
  - Same hash: nothing is pushed. The release notes say `TestAdapter: unchanged
    (latest x.y.z)`.
  - Different hash and a higher `<Version>`: pushed after the GitHub release.
  - Different hash and the same `<Version>`: the release fails. A bump is
    never forgotten, and an unchanged package is never re-pushed.
- **Who bumps.** A person bumps `<Version>` in `DevTools.TestAdapter.csproj`
  when a change touches the packed content (the same hand edit as today). The
  gate turns the existing convention into a check. The package major rises
  only when its `Min` rises (§3).
- **Where compatibility is written.** The package embeds its `{ current, min }`
  per wire. Each release's notes list the product's ranges next to the latest
  adapter, so a user can read which pairs work. The error message (§4) carries
  both versions.
- `PublishTestAdapter.yml` stays as a manual dispatch for a re-push or a hotfix
  and runs the same hash check.
- The `0.x` line stays listed on nuget.org. It is valid by §3 while `Min = 2`
  holds.

### 6. Satellites outside this repo declare, they do not follow

`RevitDevTool.PyTest` and `McpGateway` keep their own version and release
train, because their source is not in this repo. They still join the range scheme:

- **PyTest** declares `PYTEST_PROTOCOL = (current, min)` in `models.py` and
  reads `Protocols["pytest"]` from `instance/info` before the first
  `tests/run`. On mismatch it fails with the §4 message. `models.py` and
  `PytestContracts.cs` carry the same protocol number, and a golden fixture
  (below) is the check that they agree.
- **McpGateway** `register` carries `daemonVersion` and a
  `tunnel: { current, min }` range; the gateway answers with its own range and
  closes the socket with a named close code when they do not intersect. The
  Daemon surfaces the remedy in its window. This ADR defines the shape; the
  gateway change is in its repo.
- **MCP clients** stay on the spec date and SDK negotiation (0039). Not
  touched.

### 7. One way to read the product version

A single accessor returns the informational version (suffix kept, `+metadata`
removed) and replaces `GetName().Version` in `ShellViewModel`, `SentryTelemetry`,
`TestHelloResponse`, and `InstanceInfo`. A release gate fails the build if any
shipped binary reports a different informational version than the Daemon.

### 8. The lag window is closed, not only reported

The Daemon does not apply a **major** version bump (Velopack swap) while a
selected payload is pending or still on the old major, because that is the one
case where §3 gives no guarantee. Inside a major, §2 makes the lag window safe
and the user only sees the `restart-host` message if a range ever diverges.
This is the mirror of `WaitsForDaemon` (0052) and amends 0049 when it lands.

### 9. Proof, not convention

- **Golden fixtures** (extend `TestingProtocolGoldenTests`): the frozen JSON of
  every message of each shipped `Current` is committed. A change that fails a
  frozen fixture is, by §3, a `Current` bump or a rejected change.
- **Cross-version matrix in CI** (`Compile.yml` or its own workflow): current
  host ↔ the latest published adapter nupkg, the previous stable adapter, and
  the latest `revitdevtool_pytest`, over the in-process pipe. This is the test
  that would have caught problems 1–2 above.
- **Release gate**: `Min` may not rise on a non-major tag; a changed adapter
  content hash needs a higher `<Version>`, and an unchanged hash pushes nothing
  (§5).

### 10. The newer side explains, and both directions notify the user

A side can only explain what it knows, and a shipped older side cannot be
taught afterward. So **the side with the higher `Current` carries the message**;
the older side only has to avoid hiding the other's message.

| Case | Detected by | Reaches the user through | User action |
|------|-------------|--------------------------|-------------|
| **Client older than host** (adapter, pytest) | Host, at `hello` (testing) or first `tests/run` (pytest) | 1. The host-authored `ErrorMessage`, shown by the client in its own output. 2. A Daemon toast and an Updates-tab row | Update the package |
| **Client newer than host** | Client, from the host reply | The client's own output: a typed exception with the §4 message. Optional: start the Daemon Updates tab | Update RevitDevTool |
| **Daemon older than gateway** | Gateway, at `register` | `register_ack` with `remedy`, then a named close code. The Daemon raises a toast | Update RevitDevTool |
| **Gateway older than Daemon** | Daemon, from `register_ack` | A quiet "remote access unavailable" state in the Daemon window. No user action exists | None. The service owner deploys (§10.3) |

What this relies on, read from code on 2026-10-31:

- **Old clients already show the host's text.** `TestPipeClient` rethrows
  `response.ErrorMessage` for `ProtocolIncompatible` (`TestPipeClient.cs:231`).
  `revitdevtool_pytest 0.5.0` turns any error response into a
  `CollectionError(message=response.error_message)` (`bridge.py:211`), so it
  prints in the pytest report. The legacy response also carries
  `{ requested, expected }`, from which a new client can compute the remedy
  when the host is the older side.
- **The Daemon can already be pointed at its Updates tab.**
  `IpcConstants.DaemonArgs.ShowUpdates` (`--show-updates`) opens it
  (`App.xaml.cs:41`). There is no control-pipe command for it today.

#### 10.1 Contract for host-authored text

`ErrorMessage` of `ProtocolIncompatible` is a stable, plain-text contract
because it is the only channel to clients that cannot be changed. It is one or
two complete sentences that say which side is old, the two versions, and one
action (package id, version needed or "the latest", the command to run). It
never asks the user to compare ranges. Example:

`RevitDevTool 5.0.0 no longer accepts RevitDevTool.TestAdapter 0.1.4. Update the package: dotnet add package RevitDevTool.TestAdapter (latest 1.0.0).`

The structured `details` (ranges, versions, `remedy`) are additional, never
required to read the message. The other shapes are in the §4 table.

#### 10.2 What each party adds

| Party | Adds |
|-------|------|
| **NuGet adapter** | Its package version and `{ current, min }` in `hello` (`ClientKind`, `ClientVersion`). A `TestProtocolIncompatibleException` with `Remedy`, both versions, both ranges, and a formatted message. For a legacy host reply (`requested`/`expected` only) it infers the host range as `[expected..expected]`. Opening the Daemon Updates tab is off by default and enabled by `DEVTOOLS_TESTING_OPEN_UPDATES=1`, so CI never shows a window |
| **Pytest plugin** | `PYTEST_PROTOCOL`, `__version__` as `ClientVersion` in the first `instance/info` call. On a conflict `pytest.exit` with the §4 message, and a one-line fix (`uv add -U revitdevtool_pytest`). Works unchanged against 0.5.0 hosts through the host-authored text above |
| **McpGateway** | `register` carries `daemonVersion` and `tunnel { current, min }`. The gateway answers `register_ack { gatewayVersion, tunnel { current, min }, remedy? }`. On no overlap it closes with a named code (for example `4001 incompatible`; today it closes with `1000` and a reason string). Message names are constants beside the existing `Msg` set. `docs/api.md` documents them |
| **Host add-in** | Ranges and `ProductVersion` in `hello`/`instance/info`; the §10.1 text. On a rejected client, one `control/compat_conflict { clientKind, clientVersion, remedy }` to the Daemon (a one-line command with no reply, as in 0043) |
| **Daemon** | `UpdateNoticeKind.ClientIncompatible` and `ProductIncompatible` in `IUpdateNotifier`. A toast with the same text as the client output. Dedupe by `(clientKind, clientVersion, productVersion)` once per day, like `_offeredVersion` for updates (0050). No toast without an interactive desktop session. An Updates-tab list of companion clients seen (kind, version, status) |
| **Release pipeline** | `update-manifest.json` gains `clients: { testAdapter: { latest }, pytest: { latest } }`, read from nuget.org and PyPI at release time. It is shown in messages and never used to decide compatibility (§1) |

#### 10.3 Gateway deployment order

The gateway is hosted, so its owner, not the user, "updates" it; a gateway
older than a shipped Daemon is a service defect. Deploy the gateway before
releasing a Daemon that raises `tunnel.current`. Keep `tunnel.min` unchanged
for at least one product major, and raise it only after §10.4 shows no Daemon
older than the new `min`.

#### 10.4 Telemetry

Each conflict is a metric (`clientKind`, `clientVersion`, `productVersion`,
`remedy`; nothing else beyond the 0045 rules), not an exception, so it opens no
Sentry issue. It tells a maintainer when `Min` can rise. Review it against 0045
before it ships.

## Alternatives Considered

1. **Keep `==` and always release the nupkg with the product.** Closes problem 5
   but not 1, 2, 3, or 4, and pushes identical packages when only the product
   changed. A user on a stale adapter after a bump still gets a message with
   no remedy, and nothing allows a non-breaking change.
2. **Gate on product version (`MinProductVersion` in the handshake).** One
   number, but it ties compatibility to release ordering and to SemVer
   prerelease rules, and it forces a version bump of every satellite for every
   product release.
3. **Nupkg version equal to the product version (lockstep).** One number for
   the set, but it publishes unchanged packages and implies a change that did
   not happen (§5).
4. **A compatibility matrix in `update-manifest.json`.** A fourth source of
   truth beside the ranges.
5. **Feature flags per capability instead of integer ranges.** More precise,
   and more to maintain for six wires with one or two peers each. Reconsider
   only if a wire gets many independent optional features.
6. **Make the runner pass through any protocol and let the host decide.** The
   runner would still need to speak the adapter's leg. Keeping the negotiation
   per leg (§2) keeps the runner honest about what it emits.

## Consequences

Positive:

- Additive changes stop being breaking; within a stable major any adapter,
  runner, host, and pytest interoperate (the `0.1.x` adapters keep working
  through `4.x`).
- A mismatch ends with an instruction ("update the package", "update
  RevitDevTool", or "close Revit"), and logs hold both product versions.
- A release that changes neither the adapter nor the testing wire publishes
  nothing to NuGet. Pytest and the gateway get a gate at a small cost.

Tradeoffs:

- Two numbers per wire and a rule for when each changes; a contributor must
  classify a wire change (§3).
- The package number does not say which product it fits. The ranges, the §4
  message, and the release notes do.
- The content hash must ignore timestamps and build metadata. NuGet versions
  are immutable, so a bad push is fixed with the next version.
- `Min = 2` through `4.x` keeps old shapes alive and tested until a later major,
  and the §8 hold can delay a major Daemon update until Revit closes.
- Changes cross three repos (`RevitDevTool`, `RevitDevTool.PyTest`,
  `McpGateway`); this repo can only define and test its side.

## Follow-Up

Not verified in this ADR (read before implementing):

- Whether payloads use `JsonUnmappedMemberHandling.Disallow` or a source-gen
  option that rejects unknown members. §3 depends on them being ignored.
- What `TestingProtocolGoldenTests` covers today. (Checked 2026-10-31:
  `revitdevtool_pytest 0.5.0` does not call `instance/info`.)
- How Visual Studio Test Explorer and `dotnet test` render the adapter's
  exception text, and whether the MTP exit code can carry the remedy.
- Whether the host can send `control/compat_conflict` to a Daemon older than
  4.0 (it cannot; the toast is then skipped and the client output still shows).
- Whether `UpdateNotice` needs new fields for a client kind and version, or
  `Message` is enough.
- Whether the host `McpServer` fills `serverInfo.version`. If it does, the
  Daemon ↔ host MCP wire can take its `ProductVersion` from `initialize`
  without a new field.
- The exact `Min` for the pytest and tunnel wires on the first release (this
  ADR assumes `Current = Min = 1` at first, `testing` stays `2/2`).

Implementation order, each step shippable alone:

1. Single version accessor (§7); fix `ShellViewModel` and telemetry.
2. Range type and negotiation in `DevTools.Testing.Transport`; `hello` and
   `ProtocolIncompatible` fields (§2, §4). Keep `Current = 2, Min = 2` at first
   so nothing changes on the wire.
3. Golden fixtures and the cross-version CI matrix (§9).
4. `instance/info` fields and the pytest gate (§4, §6).
5. Content-hash gate for the adapter in `PublishRelease.yml` (§5).
6. Tunnel `register` ranges, with the gateway repo (§6).
7. Daemon hold on a major bump, with 0049 (§8).
8. Notification path (§10): host text contract, `control/compat_conflict`,
   Daemon toast kinds, the manifest `clients` block, and the adapter, pytest,
   and gateway messages. Start with the host-authored text (§10.1) because it
   reaches already shipped clients.

Update `docs/architecture/Testing/README.md` (the NuGet row currently says
"Independent of installer GitVersion") and `docs/agents/mcp-pytest-bridge.md`
when steps 2–5 land.
