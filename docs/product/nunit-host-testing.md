# NUnit Host Testing

Experimental in-host NUnit for Revit / AutoCAD-family via DevTools Named Pipe +
VSTest adapter. Local `testhost` discovers tests; the live host executes them.

## Status

**Experimental.** Discover / run / progress work end-to-end. The public NuGet
package is **not published** yet — consume from source / local pack only until
the first supported release.

Host-process debugging (Test Explorer “Debug”, `--debug wait`, DTE attach) is
**deferred** and rejected/ignored in this release.

## Modules

| Project | Role |
|---------|------|
| `DevTools.NUnit.Core` | `nunit/*` wire contracts, timing, protocol version |
| `DevTools.NUnit.Host` | Reflective runner, assembly preflight, bridge handler |
| `DevTools.NUnit.Runner` | CLI controller: find/launch host pipe, discover/run |
| `DevTools.NUnit.TestAdapter` | VSTest discoverer/executor (proxies to Runner) |

Sample: `samples/DevTools.NUnit.SampleTests` (import adapter props/targets,
declare `<HostName>` / `<HostVersion>`).

## Behavior

- Discover locally (metadata / portable PDB navigation); execute in host.
- Pipe: `DevTools_{Host}_{Version}_{PID}` (same control pipe family as pytest,
  **not** `DevToolsMcp_*`).
- Methods: `nunit/hello`, `nunit/discover`, `nunit/run`, `nunit/cancel`,
  `nunit/progress`.
- In-host: stamp-keyed **shadow copy** + `LoadFile` for test probe DLLs; pin
  `nunit.framework` major.minor beside the test assembly. Deploy-folder DLLs
  stay on `AssemblyLoader` (LoadFrom/ALC) — never shadow MahApps/UI.
- Attribute subset (v1): `[Test]` / `[TestCase]`, SetUp / TearDown /
  OneTimeSetUp / OneTimeTearDown. Not full NUnit.Engine.

## CLI

```text
DevTools.NUnit.Runner discover <assembly> --host Revit --version 2024
DevTools.NUnit.Runner run <assembly> --host Revit --version 2024 [--filter ...]
```

Runner ships under the ApplicationPlugins bundle `Contents` folder (publish
`DevTools.NUnit.Runner`).

## Relation to ricaun.RevitTest

| Topic | ricaun.RevitTest | DevTools.NUnit |
|-------|------------------|----------------|
| In-host engine | `ricaun.NUnit` reflective (`TestEngine` / attributes) | Reflective runner; **no** `NUnit.Engine` in host |
| Probe load | **Also shadows**: zip test folder → `%TEMP%\RevitTest\` → extract → `Assembly.LoadFile` on the temp copy (then optional zip-back) | Stamp-keyed per-file shadow via `DirectoryAssemblyLoad` → `LoadFile` |
| Transport | Own Console + `PipeTestServer`/`PipeTestClient` (process-named pipe) | Existing `DevToolsPipeServer` + `IHostContextExecutor` |
| IDE surface | VS-oriented + EnvDTE attach | VSTest proxy; debugging deferred |
| Package | ricaun NuGet ecosystem | Intended: one `DevTools.NUnit.TestAdapter` NuGet (**unpublished**) |
| Hosts | Revit-focused product | Revit + AutoCAD family on shared DevTools platform |

**Conflict / coexistence (what actually breaks):**

- ricaun does **not** wait on RevitDevTool. Console waits for **its own** Application
  plugin pipe (`PipeTestClient` ↔ in-Revit `PipeTestServer`). If that pipe never
  appears (plugin not loaded into the chosen Revit process, startup dialogs, hang),
  the run loops until timeout — easy to misread as “waiting for DevTools”.
- Reusing an already-open Revit that started **before** ricaun installed its
  ApplicationPlugins bundle means the ricaun add-in is missing until that process
  is restarted / `NUnit.Open` forces a new Revit.
- Both can load different `nunit.framework` identities in the same Revit (Dynamo,
  DevTools host, ricaun Application). DevTools avoids `NUnit.Engine`
  `FrameworkController` for that reason.
- VSTest can load **both** adapters if a project references both; wrong adapter
  may own the run (DevTools Runner vs ricaun Console).

Do not depend on `ricaun.NUnit` / `ricaun.RevitTest` packages for DevTools NUnit.

**DevTools advantages:** shared pipe/DI/execution guard with pytest and MCP;
stamp-keyed per-file shadow (no whole-folder zip round-trip); multi-host Runner
options; one intended adapter package aligned with the rest of RevitDevTool.

## NuGet

| Package | Publish status |
|---------|----------------|
| `DevTools.NUnit.TestAdapter` | **Not published** — experimental; local/source only |
| Core / Host / Runner | Not consumer NuGet APIs |

When publishing later: pack the TestAdapter only; document Runner install path
and required `<HostVersion>` in test projects.

## Gaps (not done)

- Host-process **debugging** (attach + breakpoint bind with shadow load)
- Microsoft Testing Platform (MTP) adapter mode
- Rider-specific proof / packaging beyond VSTest proxy
- Full NUnit attribute matrix (Theory, explicit, categories, parallel, …)
- Broader automated host-matrix CI (years × hosts) for NUnit beyond sample smoke
- Public NuGet feed + versioning / changelog for the adapter
- Deeper unit coverage for TestAdapter execute path (layout/source-assert tests only today)
- Guard so a machine-local / VSTest-discovered adapter does not hijack unrelated `dotnet test` runs (can yield exit code 1 when Runner is invoked on non-NUnit assemblies)
- Optional: debug-mode load-from-bin (no shadow) if debugging returns

## Related

- Decision: [`docs/decisions/0015-nunit-host-testing-standard-integration.md`](../decisions/0015-nunit-host-testing-standard-integration.md)
- Agent notes: [`docs/agents/nunit-host-testing.md`](../agents/nunit-host-testing.md)
- Pytest sibling: [`pytest-bridge.md`](pytest-bridge.md)
