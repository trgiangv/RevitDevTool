# 0055 Product Version, Wire Contracts, and Satellite Compatibility

Date: 2026-10-29

## Status

**Proposed 2026-10-29, amended 2026-11-02 to match the implementation.** Builds on the auto-update set
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

### 2. Range overlap replaces exact equality

Each endpoint of a wire declares `Current` and `Min` with `Min ≤ Current`.

```
compatible(A, B)  = A.Min <= B.Current  and  B.Min <= A.Current
```

If not compatible, the side whose `Current` is below the other's `Min` is
**too old**; that side is the one to update (§4). A newer side that accepts an
older one speaks the older shape; there is no negotiated number on the wire.

**One `sender` per message.** Every checked message carries the side that sent
it, in one shape:

```json
"sender": { "version": "4.0.0-beta.5", "protocol": { "current": 1, "min": 1 } }
```

`ProtocolRange`, `ProtocolSender`, and `ProtocolPeer` (the name and update
message of a peer) live in `DevTools.Ipc`, the JSON layer every checked wire
already uses.

**Shipped peers.** Messages from before this ADR carry `protocol_version` and
no `sender`. They are read as too old and get the §4 sentence. The
`protocol_version` field is gone; there is no legacy path.

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
- **Every range starts at `1`** (testing, pytest, tunnel). Shipped peers are
  rejected for the missing `sender`, so their old `protocol_version 2` does not
  count; no major has changed a protocol yet.
- **Unknown members are ignored and defaults are explicit** on every
  `BridgeMessage` payload and `TestRunExecute`/`TestRunnerStreamMessage`. This
  is what makes "new optional field" safe. Golden tests (below) enforce it.

### 4. Each side checks the other and names the update

- **Testing `hello`.** The request and the response each carry `sender`. The
  host checks the runner; the runner checks the host. `testing/run` carries no
  version: `hello` already decided.
- **Adapter → runner.** `TestRunExecute` carries `sender`; the runner checks it
  before it starts a host.
- A missing `sender` reads as too old.
- One call per message type answers the check:
  `TestingProtocol.Mismatch(message)` and `PytestProtocol.Mismatch(request)`
  return `null`, or one sentence: `<A> is not compatible with <B>. <update message
  of the older side>`. The update messages are "Update RevitDevTool.TestAdapter to the
  latest version.", "Update RevitDevTool to the latest version.", and "Restart
  the host app to load the latest RevitDevTool."
- `ProtocolIncompatible` carries the sentence as its message and no details.

The product version is shown in the sentence and logged. It is never compared.

### 5. The NuGet package has its own version, bumped by hand

A product release often changes neither the adapter nor the testing protocol.
`RevitDevTool.TestAdapter` keeps its **own SemVer**, separate from the product
version. Compatibility with the product is the range check (§2), not the
package number.

A person decides when the adapter changed enough to publish, bumps `<Version>`
in `DevTools.TestAdapter.csproj`, and runs `PublishTestAdapter.yml`. No release
step decides it: whether a change matters to consumers is a judgment, and the
packed assemblies are not byte-stable across builds (`+<sha>`, ILRepack), so
nothing in the build can tell it reliably. The adapter is bumped once per
coherent change, not per pin move; it stays `0.1.4` until this ADR is
implemented. Shipped `0.1.x` adapters send no `sender`, so the release that
ships this ADR needs a new adapter published with it.

### 6. Satellites outside this repo declare, they do not follow

`RevitDevTool.PyTest` and `McpGateway` keep their own version and release
train. They join the range scheme:

- **PyTest.** `PROTOCOL_CURRENT` and `PROTOCOL_MIN` in `constants.py` mirror
  C# `PytestProtocol`. Right after it connects, the plugin reads
  `protocols.pytest` from `instance/info` and ends the session with
  `pytest.exit` and the §4 sentence on a mismatch. Each `tests/run` and
  `ipytests/run` also sends `sender`, and the host checks it. A host without
  `protocols`, and a request without `sender`, read as too old.
- **`instance/info`** adds `productVersion` and `protocols`. It lists only
  `pytest`: testing clients get their range from `hello`, and
  `DevTools.Execution` does not reference the testing transport.
- **McpGateway.** `register` carries `sender`. When the ranges do not overlap,
  or `sender` is missing, the gateway
  closes the socket with code `4001` and a one-sentence reason of at most 123
  bytes. There is no `register_ack`: after `register` the socket carries
  JSON-RPC to the Daemon's MCP server, so a gateway frame would land in that
  stream. On `4001` the Daemon logs the reason, stops reconnecting, and shows
  "Not compatible with the gateway". `/machines` reports each `product_version`
  from `sender.version`.
- **MCP clients** stay on the spec date and SDK negotiation (0039).

### 7. One product version per process

`AppVersion.Current` in `DevTools.Utilities` (no dependencies) is the
informational version without `+metadata` (`4.0.0-beta.4`). The build stamps
one version on every assembly of a release; the adapter merges
`DevTools.Utilities`, so there it is the package version. `DevTools.Telemetry`
references only Hosting and Sentry (0018), so it reads the same attribute from
its own assembly. The version feeds `sender`, `instance/info`, the Sentry release, and the titles
the user sees: the Daemon window, the Revit dock pane and floating window, and
the AutoCAD palette (`DevTools 4.0.0-beta.4`).

### 8. The lag window is closed, not only reported

A Daemon update to a higher **major** waits while an add-in of that major is
staged and waiting for its host to close (`UpdateEngine.HoldsMajorUpdate`),
because two majors may not share a range. An add-in the new release does not ship,
or one that failed to download, does not hold the Daemon. The user sees "Close the host apps to finish the update."
The add-in updates when the host closes, and the next cycle swaps the Daemon. A
minor update does not wait. This is the mirror of `RequiresNewerUpdater` (0052).

### 9. Proof, not convention

- **Shipped-peer fixtures** (`ShippedPeerTests`): the frozen messages of
  TestAdapter `0.1.4` and TestRunner `4.0.0-beta.4` get the exact §4 sentence
  that names their update.
- **Cross-version runs** need a host app, which CI does not have. They are run
  by hand before a release: the shipped runner and the shipped pytest plugin
  against the new add-in.


### 10. The newer side explains

A shipped older side cannot be taught afterward, so **the side that knows both
ranges writes the sentence**, and the older side only prints it.

| Case | Detected by | Reaches the user through |
|------|-------------|--------------------------|
| Client older than host (adapter, runner, pytest) | Host | The host sentence in the client output. Shipped clients already print it: `TestPipeClient` rethrows `ErrorMessage`, pytest `0.5.0` turns it into a collection error |
| Client newer than host | Client | The client's own sentence |
| Daemon and gateway | Gateway | Close code `4001`; the Daemon status and log |

#### 10.1 The sentence is a contract

The sentence is the only channel to clients that cannot change, so it stays
plain text, one sentence plus one update message, and never asks the user to
compare ranges.

#### 10.2 Gateway deployment order

The gateway is hosted, so its owner, not the user, updates it. Deploy the
gateway before releasing a Daemon that raises the tunnel `current`. Keep the
tunnel `min` unchanged for at least one product major.

The release that adds `sender` is the reverse. A gateway from before it ignores
`sender`, and a gateway with the check rejects every Daemon without one; those
Daemons do not know `4001` and keep reconnecting. Release the Daemon first,
let installed Daemons update, then deploy the gateway.

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
   not happen.
4. **A compatibility matrix in `update-manifest.json`.** A fourth source of
   truth beside the ranges.
5. **Feature flags per capability instead of integer ranges.** More precise,
   and more to maintain for six wires with one or two peers each. Reconsider
   only if a wire gets many independent optional features.
6. **A Daemon notification for a rejected client** (a control command, a toast,
   the latest client versions in `update-manifest.json`). The user already sees
   the sentence in the client output when the run fails, so it repeats the same
   message through more moving parts.
7. **Make the runner pass through any protocol and let the host decide.** The
   runner would still need to speak the adapter's leg. Keeping the check
   per leg (§2) keeps the runner honest about what it emits.
8. **Keep reading `protocol_version` from shipped peers.** Keeps `0.1.x`
   adapters working, but leaves two shapes and a negotiated number on every
   message. One `sender` shape and one sentence for anything older is simpler.

## Consequences

Positive:

- Additive changes stop being breaking; within a stable major any adapter,
  runner, host, and pytest that send `sender` interoperate.
- A mismatch ends with one sentence that names the update to make.
- A product release does not force an adapter release. Pytest and the gateway
  get a check at a small cost.

Tradeoffs:

- Two numbers per protocol and a rule for when each changes; a contributor must
  classify a protocol change (§3).
- The package number does not say which product it fits. The ranges and the
  sentence do.
- Bumping and publishing the adapter is a manual step that can be forgotten.
- Shipped adapters (`0.1.x`), runners, pytest plugins (`0.5.x`), and Daemons
  stop working with the release that ships this ADR; they get the sentence, not
  a fallback.
- The §8 hold can delay a major Daemon update until the hosts close.
- Changes cross three repos (`RevitDevTool`, `RevitDevTool.PyTest`,
  `McpGateway`).

## Follow-Up

- Progress and verification: [the plan](../plans/active/2026-10-31-test-stack-bump-and-versioning-0055.md).
- A cross-version CI run needs a runner with a host app.
- No check stops `Min` from rising on a non-major tag.
- Not verified: how Visual Studio Test Explorer shows the adapter's sentence.
- Update `docs/architecture/Testing/README.md` (the NuGet row) and
  `docs/agents/mcp-pytest-bridge.md` when this ADR is accepted.
