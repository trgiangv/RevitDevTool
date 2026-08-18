# Assembly Isolation Kernel Implementation Plan

Date: 2026-08-18
Status: Completed

## Goal

Replace feature-specific assembly loaders with one host-neutral
`DevTools.AssemblyIsolation` kernel while preserving add-in, execution, MCP, and
NUnit product behavior on net48, net8, and net10.

The lasting behavior is defined by
[the product contract](../../product/assembly-isolation.md). Architectural
rationale and rejected alternatives live in
[decision 0023](../../decisions/0023-shared-assembly-isolation-kernel.md).

## Constraints

- Bind shared contracts through concrete assemblies and full identity.
- Keep workload-local dependencies private and lazy.
- Do not introduce `System.*`, `Microsoft.*`, Autodesk, or UI prefix sharing.
- Keep compilation, discovery, registries, invocation, generation, result
  mapping, and logging in their owning features.
- Keep NUnit net48 in the host default AppDomain (`ScopedNetFramework` +
  shadow `LoadFile`). Do not create a child AppDomain; Revit cannot unload
  mixed-mode host APIs.
- Preserve existing ILRepack, Polyfill, package-public API, and host deployment
  policy.
- Discovery and verification must not launch or contact an Autodesk host.

## Delivery Scopes

### 1. Shared isolation kernel

- Add the multi-target leaf project with full-identity matching, concrete parent
  bindings, lazy managed/native sources, path and reparse-point containment,
  stream loading, metadata-only sessions, and structured diagnostics.
- Support permanent, collectible, and scoped-net48 lifetimes without claiming
  unsupported net48 unload behavior.
- Add modern and net48 contract, identity, containment, lifecycle, and unload
  tests.

### 2. Add-in and command execution

- Replace permanent add-in, command, C# script, discovery, PyRevit, and
  PythonNet stub-generator loaders with feature-owned isolation plans.
- Pass actual Revit/AutoCAD boundary assemblies rather than ambient API-name
  registries.
- Preserve command results, script compilation, cleanup ordering, private
  dependency versions, and source-file unlock behavior.

### 3. MCP toolsets and metadata

- Replace MCP's broad-prefix load context with a collectible isolation plan.
- Keep schema, registry, dispatcher, and invocation ownership in MCP.
- Clear dispatcher references before session disposal and retain a scoped net48
  sibling resolver.

### 4. NUnit host runtime and MTP package

- Replace modern NUnit generation loaders with manifest-backed isolation plans.
- Preserve exact generation-selected `nunit.framework` binding and Dynamo
  conflict protection.
- Keep net48 generations in the host default AppDomain, isolate same-identity
  copies via shadow `LoadFile`, and keep tests on the host calling thread.
- Keep the MTP provider closure private, validate its bootstrap resolver by full
  identity, and prove clean consumers across supported TFMs.

### 5. Remove legacy ownership and enforce the boundary

- Delete assembly-loading ownership from `DevTools.Utilities` and remove old
  host-name/shared-prefix policies.
- Add a repository architecture guard for direct runtime/metadata loaders.
- Document the single MTP bootstrap exception and verify host/package payloads
  contain one intended kernel/contract identity.

## Verification

- AssemblyIsolation modern and net48 suites cover identity, lazy resolution,
  managed/native containment, metadata-only loading, hook cleanup, and unload.
- Execution tests cover transitive dependencies, conflicting private versions,
  bridge lifetime, and command success/error parity.
- MCP tests cover modern and net48 toolset loading, metadata parsing, cache
  ordering, diagnostics, and packaging.
- NUnit tests cover modern/net48 isolation, cancellation, result semantics,
  runtime unload, MTP bootstrap identity, and fresh clean-consumer packages.
- Revit and AutoCAD projects compile with deployment disabled; no Autodesk host
  is launched, contacted, or deployed.

Known pre-existing limitations remain separate: the MCP Python sample annotation
drift and the Autodesk whole-solution multi-target output collision.
