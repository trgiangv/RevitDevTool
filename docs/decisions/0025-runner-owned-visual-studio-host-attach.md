# 0025 Runner-Owned Visual Studio Host Attach

Date: 2026-08-27

## Status

Accepted. Amends [0016](0016-nunit-native-runtime-and-mtp-first-integration.md)
decision 11: EnvDTE lives in `DevTools.TestRunner.Core/Debugging/`. Does not
reopen 0016 alternative 6 (no generic debugger *wire* protocol).

## Context

Host tests execute inside Autodesk `Revit.exe` / `acad.exe`. Visual Studio
Test Explorer **Debug** attaches to the MTP testhost (`OutputType=Exe`), not
to the Autodesk process. Breakpoints in test bodies miss unless a second
attach targets the host PID **after** the control pipe is up and **before**
`testing/run`:

```text
Test Explorer Debug
  → testhost (VS already attached here)
    → DevTools.TestRunner --debug-parent-pid <testhost>
      → EnsurePipe
      → EnvDTE Process.Attach(hostPid)
      → testing/run
      → Detach
```

Visual Studio cannot **Run** tests while the operator is already attached to
the host — Test Explorer **Debug** is the only VS path, and it attaches the
testhost. That is why Runner EnvDTE-attaches VS to the host PID.

Rider and C# Dev Kit **can** attach the Autodesk host and still **Run** tests
(attach does not block the test-execute flow). That is enough to hit
breakpoints in host test bodies. Runner does not attach those IDEs.

Code: `ExecutionCoordinator` + `DebugAttachScope` + `VisualStudioAttach`.
MTP/adapter only sets `DebugParentPid` when `Debugger.IsAttached`
(`HostTestFramework.ApplyDebugParent`). Architecture tests forbid EnvDTE in
the adapter.

`FindDte` returns the first DTE debugging `parentProcessId`, **else any DTE**,
**else** `GetActiveObject`. Narrowing (parent PID with no match ⇒ skip)
broke Test Explorer Debug and is withdrawn. `samples/ricaun.NUnit.SampleTests`
does not match parent PID — `GetActiveObject` then `LocalProcesses.Attach`.

## Decision

### 1. Visual Studio attach stays Runner-owned; the host does not drive IDEs

`DevTools.TestRunner` is the only process that may call EnvDTE to attach to
an Autodesk host PID. The add-in, `DevTools.Execution`, Python initializer,
MCP daemon, and MTP adapter must not `Process.Start` an IDE, mutate
`launch.json`, or call EnvDTE.

- Adapter/MTP: if `Debugger.IsAttached`, pass `--debug-parent-pid` (testhost
  PID). That flag still implies debug.
- CLI: `--debug` and `--debug-parent-pid` stay. Presence of parent PID
  implies debug.
- Timing: `EnsurePipeAsync` → attach → provider `testing/run` → dispose
  (EnvDTE `Detach`).
- Attach failure warns on stderr and the run continues.
- No `debug-ready` handshake on the host pipe.
- File-backed generations (0016 decision 12) stay the symbol story.

### 2. Hook is VS attach, not an IDE protocol

`IDebuggerAttach` is the Runner-local rename of `IVisualStudioAttach`
(keep `VisualStudioAttach.cs`). It is not a wire protocol and not an
abstraction for Python or for other IDEs.

`FindDte` prefers a DTE whose `DebuggedProcesses` contains the testhost
PID, then any ROT DTE, then `GetActiveObject` (`VisualStudio.DTE` 23→9).
Test Explorer always passes `--debug-parent-pid`, and `DebuggedProcesses`
often does **not** list the testhost.

```text
AttachTarget(int HostProcessId, int? ParentProcessId, string? AssemblyPath)

IDebuggerAttach
  TryAttach(AttachTarget, warnings) : bool
  TryDetach(hostProcessId, warnings)
```

Register `VisualStudioAttach.Instance` in `DevTools.TestRunner`. Do not
register a composite for other IDEs.

| Backend | Mechanism | Confirm | Detach |
|---------|-----------|---------|--------|
| `VisualStudioAttach` | ROT + EnvDTE `Process.Attach`. Prefer DTE debugging `parentProcessId`, else any ROT DTE, else `GetActiveObject`. | EnvDTE `DebuggedProcesses` (15s) | EnvDTE `Process.Detach(false)` |

### 3. Operator attach is the product path outside Visual Studio

Attach the Autodesk host **first**, then **Run** tests (not Test Explorer
**Debug**). Debug-from-testhost still misses in-host breakpoints; the
difference from Visual Studio is that these IDEs allow Run while attached.

| IDE | What to attach | How |
|-----|----------------|-----|
| **Rider** | Host PID (`Revit.exe` / `acad.exe`) | Rider Attach to Process — same PID pick as Visual Studio, then **Run** tests. No repo config file. |
| **C# Dev Kit** (VS Code only) | Host PID | `.vscode/launch.json`: `Attach Host: NetFramework` (`clr`, Revit/AutoCAD 2022–2024 / net48) or `Attach Host: NetCore` (`coreclr`, 2025+). `${command:pickProcess}`. C# Dev Kit is not a VS Code-fork debugger. |
| **VS Code and forks** (Cursor, …) | Python `debugpy` port | `.vscode/launch.json`: `Attach Host: Python` (`debugpy`, `localhost:5678`). Host already listens (`PythonDebugger`, preferred port 5678). CLR/`coreclr` attach does **not** debug Python locals. |
| **PyCharm** (and Rider Python) | Python `debugpy` port | `.run/Attach.run.xml`: `Attach Host: Python` (`PythonDapAttachConfiguration`, `localhost:5678`). |

Do not add Runner `IDebuggerAttach` backends, `launch.json` mutation, or a
JetBrains SDK for these paths.

### 4. Confirmation and detach are Visual Studio EnvDTE

Do not replace VS confirmation with `CheckRemoteDebuggerPresent` (cannot
name which IDE attached). Operator attach/detach is the IDE’s own session.

### 5. What this does not decide

- Python `debugpy.listen` / `IDebuggerBridge` internals (port, not PID).
- Host-side `IDebugController`.
- MCP execute / interactive C# script attach (no testhost, no Runner).
- Changing VS warn-and-continue.

## Alternatives Considered

1. **Host-side `IDebugController`.** Rejected: wrong owner.
2. **Unify with Python `debugpy.listen`.** Rejected: different runtime / DAP.
3. **Generic host-pipe `debug-ready`.** Rejected (0016 alternative 6).
4. **Fail the test run when attach fails.** Rejected: warn-and-continue.
5. **Narrow `FindDte` so a parent PID never falls through to any DTE.**
   Withdrawn: Test Explorer Debug always sets `--debug-parent-pid`;
   `DebuggedProcesses` frequently omits the testhost, so the narrowing
   skipped attach entirely. Restore prefer-match-then-any / `GetActiveObject`.

## Consequences

Positive:

- VS Test Explorer Debug keeps attach-before-`testing/run` and detach-after.
- Rider / C# Dev Kit keep operator attach + **Run**; Python keeps listen-on-port.
- MTP, host pipe, and MCP stay unchanged.

Tradeoffs:

- `--debug` CLI without parent PID remains VS-any-instance.
- Interactive C# / MCP execute debug remains a named gap.
