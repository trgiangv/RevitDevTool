# Decisions

Lasting product, architecture, host-boundary, and validation choices that future
work must inherit. Task-local choices stay in `docs/plans/active/`.

Use `docs/templates/decision.md` for new entries. Index every accepted decision
here.

## How agents should read this index

- **Accepted** = inherit this policy. **Superseded** = stub; follow the pointer,
  do not reconstruct the old rule. **Proposed** = not shipped; do not treat as
  current behavior.
- Living maps live in `docs/architecture/` and `docs/product/`. Decisions hold
  the *choice*, not the module inventory.
- MCP product: **[0027](0027-mcp-product-surface.md)** (Daemon envelope, not
  full protocol). Host pipe / SDK-on-host rules: **[0012](0012-host-mcp-spec-engine.md)**
  (partially superseded by 0027 — SDK types and ILRepack allowed; no `McpServer`
  session on the host pipe).
- [0030](0030-host-owned-cpython-and-package-managers.md) is **Python runtime**.
  [0031](0031-daemon-json-source-gen.md) is STJ source-gen on Daemon wires.
  [0032](0032-daemon-mewui-and-aot.md) is Daemon HandyControl WPF; Native AOT
  is dropped.
  [0033](0033-ironpython-pydevd-debugger.md) is **Accepted** IronPython DAP
  (in-process PyDev.Debugger 2.8.0; VS Code client is `debugpy` on 4567).
  CPython stays `debugpy` on 5678 (0025).
  [0034](0034-execution-mstest-sdk-scoped-tests.md) is **Accepted** Execution
  unit tests: scoped MSTest.Sdk 4.4.1 projects + first-party `--coverage`.
  [0035](0035-mstest-sdk-repo-tests.md) is **Accepted** remaining in-repo
  `tests/*.Tests` on the same SDK and collector (no xUnit, no Coverlet).
  [0036](0036-revit-monitor-link-element-tokens.md) is **Accepted** Revit
  link tokens (`linkInstanceId@` + three kinds), host-space zoom, Element
  Finder, and `RevitDevTool.Tools` ownership of select/search + Command
  Browser. GeoViz / Inspect pick-to-overlay is out of scope.
  [0037](0037-handycontrol-replaces-mahapps.md) is **Accepted** and landed.
  HandyControl source compiles into loose `DevTools.UI`. Hosts do not
  ILRepack WPF. MahApps, ControlzEx, and Xaml Behaviors are gone.
  [0038](0038-mstest-host-provider.md) is **Accepted**. In-host MSTest 4.4.1
  with Microsoft.Testing.Platform 2.4.1 is a third provider on the existing
  testing kernel. NUnit stays the default. Live two-generation Revit proof
  is still an open plan item.
  [0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) is **Accepted**
  (implemented 2026-10-03). Daemon envelope, host `McpServer` on the pipe,
  search contract v2.
  [0040](0040-bm25f-search-and-hit-annotations.md) is **Accepted**.
  The model-facing tool is `code_mode` (C# body). BM25F search and id
  dispatch stay inside that program. `search_dynamic` and `invoke_dynamic`
  leave `tools/list`. Not current behavior. Host tools stay off `tools/list`.

## Index

| ID | Title | Status |
|----|-------|--------|
| [0001](0001-repo-owned-ai-harness.md) | Repo-owned AI harness | Accepted |
| [0002](0002-host-agnostic-platform.md) | Host-agnostic platform direction | Accepted |
| [0003](0003-architecture-docs-authority.md) | Layered documentation authority | Accepted |
| [0004](0004-hook-first-compile-harness.md) | Hook-first compile verify | Accepted |
| [0005](0005-gitnexus-indexing-limitation.md) | GitNexus indexing limitation | Accepted |
| [0006](0006-mcp-multi-host-readiness.md) | MCP multi-host readiness | Accepted |
| [0007](0007-revit-core-and-visualization-boundaries.md) | Revit.Core and visualization boundaries | Accepted |
| [0008](0008-document-bridge-startup-dialogs.md) | Document bridge and startup dialogs | Accepted |
| [0009](0009-multi-host-pytest-client.md) | Multi-host pytest client | Accepted |
| [0010](0010-daemon-sole-mcp-host.md) | Daemon is sole MCP host | Accepted |
| [0011](0011-hybrid-repository-harness-layout.md) | Hybrid repository-harness docs layout | Accepted |
| [0012](0012-host-mcp-spec-engine.md) | Host MCP spec engine (no SDK session on host pipe) | Partially superseded by 0027 |
| [0014](0014-pep723-skip-if-listed-search-first.md) | Skip-if-listed + search-first (Pixi/Pip) | Accepted |
| [0015](0015-nunit-host-testing-standard-integration.md) | NUnit host testing through standard .NET test integrations | Partially superseded by 0016 |
| [0016](0016-nunit-native-runtime-and-mtp-first-integration.md) | Native NUnit runtime with MTP-first integration | Accepted |
| [0017](0017-nunit-host-test-output-routing.md) | In-host test output routing (pane vs IDE) | Accepted |
| [0018](0018-host-identity-and-out-of-process-infrastructure.md) | Host identity and out-of-process infrastructure | Accepted |
| [0019](0019-ilrepack-and-polyfill-isolated-alc.md) | ILRepack and Polyfill on isolated load contexts | Accepted |
| [0020](0020-framework-neutral-mtp-host-testing.md) | Framework-neutral MTP host testing | Proposed |
| [0021](0021-testing-kernel-and-provider-owned-framework-runtime.md) | Testing kernel and provider-owned framework runtime | Accepted |
| [0022](0022-nunit-mtp-only-testing-stack.md) | NUnit MTP-only testing stack | Accepted |
| [0023](0023-shared-assembly-isolation-kernel.md) | Shared assembly isolation kernel | Accepted |
| [0024](0024-testing-core-open-closed-providers.md) | Testing core open-closed for providers | Accepted |
| [0025](0025-runner-owned-visual-studio-host-attach.md) | Runner-owned Visual Studio host attach | Accepted |
| [0026](0026-ironpython-unittest-script-execution.md) | One IronPython unittest flow, dialect 2.7 and 3.4 | Accepted |
| [0027](0027-mcp-product-surface.md) | MCP product surface — Daemon envelope, not full protocol | Accepted |
| [0030](0030-host-owned-cpython-and-package-managers.md) | Host-owned CPython — uv sidecar vs Pixi-owned interpreter | Accepted — Python runtime |
| [0031](0031-daemon-json-source-gen.md) | Daemon JSON is source-generated | Accepted |
| [0032](0032-daemon-mewui-and-aot.md) | Daemon desktop is HandyControl WPF; Native AOT is dropped | Accepted — amended 2026-09-27 |
| [0033](0033-ironpython-pydevd-debugger.md) | IronPython debug via vendored PyDev.Debugger 2.8.0 | Accepted |
| [0034](0034-execution-mstest-sdk-scoped-tests.md) | Execution tests: scoped MSTest.Sdk + first-party coverage | Accepted |
| [0035](0035-mstest-sdk-repo-tests.md) | In-repo tests: MSTest.Sdk 4.4.1 + first-party coverage | Accepted |
| [0036](0036-revit-monitor-link-element-tokens.md) | Revit link tokens, Element Finder, `RevitDevTool.Tools` | Accepted |
| [0037](0037-handycontrol-replaces-mahapps.md) | HandyControl compiles into loose `DevTools.UI`; WPF stays out of ILRepack | Accepted |
| [0038](0038-mstest-host-provider.md) | MSTest 4.4.1 + MTP 2.4.1 in-host provider on the existing testing kernel | Accepted |
| [0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) | MCP flow audit — SDK reuse, search contract v2, vocabulary | Accepted |
| [0040](0040-bm25f-search-and-hit-annotations.md) | `code_mode` runs C#; BM25F search stays inside it | Accepted |
