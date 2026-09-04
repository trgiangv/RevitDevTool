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
  [0031](0031-daemon-json-source-gen.md) is STJ source-gen **in support of 0032**.
  [0032](0032-daemon-mewui-and-aot.md) is Daemon MewUI + AOT target.

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
| [0031](0031-daemon-json-source-gen.md) | Daemon JSON source-gen (supports 0032 AOT) | Accepted — support for 0032 |
| [0032](0032-daemon-mewui-and-aot.md) | Daemon desktop is MewUI; Native AOT is the target | Accepted — UI shipped; AOT not production |
