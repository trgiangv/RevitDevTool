# 0019 ILRepack And Polyfill On Isolated Load Contexts

Date: 2026-08-16

## Status

Accepted

[0027](0027-mcp-product-surface.md) owns the host **wire** (SDK DTOs
allowed; no `McpServer` session on the pipe). This decision owns the **merge**:
one ILRepack pipeline for every opt-in project. MCP is not a second packing
mode. The load context and TFM are the variables.

## Context

Host add-ins merge copy-local managed DLLs into one assembly
(`props/ILRepack.targets`, opt-in `ILRepackable`). The merge uses ILRepack
`/union` because `Microsoft.Extensions.*` packages contribute duplicate types
(for example `LoggingBuilderExtensions`). Without `/union` the pack fails.

`/union` of **divergent** `Polyfills.Polyfill` copies (different package
version or feature flags) produces an image CoreCLR rejects at
`AssemblyLoadContext.LoadFromAssemblyPath`:

```text
System.BadImageFormatException: Invalid token
```

That rejection is a **.NET 10 isolated ALC** importer check. The default load
context and .NET Framework are more permissive of the same smashed metadata.
Autodesk host year is only how this repo maps to `net10.0-windows`; it is not
the defect. Polyfill is a compile-time source package: every consuming assembly
embeds `Polyfills.Polyfill`. Identical copies from the same package may merge;
a foreign copy stays a sidecar.

`ricaun.ILRepack` is a wrapper around the same `ILRepack.exe` (2.0.46). It does
not change `/union` semantics and is not a substitute for this policy.

Unmanaged Scintilla/Lexilla satellites are copied under
`runtimes/win-x64/native/` (`DevTools.Logging` direct `Scintilla5.NET` reference
plus `KeepScintillaWinX64Native` in `props/Common.props`). They are not
copy-local at output root, so they are not ILRepack inputs.

MCP follows that same input rule, split by who loads the assembly. .NET
toolsets compile against MCP (`ExcludeAssets=runtime`) and do not ship it.
Catalog loads them in a collectible ALC whose isolation plan `Pin`s MCP
contract assemblies from the host default context. Host ILRepack embeds
copy-local MCP into the add-in DLL, removing standalone `ModelContextProtocol*`
assembly identities from the output directory. Kernel `Pin` keys shares by
**simple name** only; automatic bind from the host load context for repacked
MCP is **not implemented** today. Do not exclude `ModelContextProtocol*` by
filename.

## Decision

1. **Keep the in-repo driver.** `props/ILRepack.targets` + `ILRepackable` remain
   the merge mechanism. The driver adds the `ILRepack` PackageReference when
   that flag is true. Do not switch hosts to `ricaun.ILRepack`.
2. **Polyfill applies to every TFM.** `GlobalPackageReference` Polyfill is
   unconditioned (repo `Directory.Packages.props`). The pythonnet stub-gen
   submodule keeps its own package graph and is outside this policy.
   `Directory.Build.props` sets `PolyUseEmbeddedAttribute` and
   `PolyArgumentExceptions` before `Polyfill.targets` evaluates (not
   `props/Common.props` — that import is too late). That enables
   `ArgumentNullException.ThrowIfNull` on net48 without `InternalsVisibleTo`
   CS0121/CS0436. Do not `GlobalPackageReference Remove="Polyfill"` on product or
   test csproj. Do not set `PolyPublic` (conflicts with EmbeddedAttribute; public
   copies CS0121/CS0433/CS0436 across ProjectReferences). Polyfill `Lock` is
   `internal` on net48/net8: **private** mutex fields may be `Lock`;
   **public/protected** mutex fields stay `object`. Do not replace an existing
   `??` with `ThrowIfNull`. TUnit's own polyfills stay off
   (`EnableTUnitPolyfills=false`) — that is a different package.
3. **`/union` is a driver default (`ILRepackUnion=true`).** Do not copy it onto
   a csproj. Do not replace it with `/allowdup` without a proven pack **and**
   isolated-ALC load of the host DLL.
4. **ILLink and Parallel stay driver defaults for every TFM.**
   `ILRepackILLink=false` (`/illink` concatenates unused XML; this repo does not
   ILLink the packed add-in) and `ILRepackParallel=true` (speed only). Do not
   flip these per `net10` or restate them on a csproj. Isolated ALC is handled
   by one Polyfill package version across our inputs plus the Nice3point
   sidecar. Do not turn `ILRepackable` off to dodge isolated ALC.
5. **Our Polyfill copies may merge; a foreign copy may not.** Every in-repo
   assembly embeds the same Polyfill 11.x sources. net8 / net10 force the same
   `Feature*` constants, so `/union` sees one type shape (host `AllowUnsafeBlocks`
   is a superset of library copies). A **different** Polyfill (Nice3point
   Extensions on net10) stays beside the add-in (`RepackBinariesExcludes`).
   Do not merge a second package version of `Polyfills.Polyfill`.
6. **ILRepack only sees copy-local managed DLLs.** What must not merge must not
   land in `TargetDir`. Scintilla/Lexilla live under `runtimes/win-x64/native/`.
   JetBrains.Annotations is compile-only (`PrivateAssets=all`,
   `ExcludeAssets=runtime` in `props/Common.props`). .NET toolsets keep MCP
   compile-only the same way. Host-loaded projects still copy-local MCP so
   ILRepack embeds it (no siblings). That merge removes standalone
   `ModelContextProtocol*` identities; toolset ALC name-based bind from the
   host load context is not implemented in the isolation kernel. None of these
   are ILRepack filename excludes.
   `RepackBinariesExcludes` is only for assemblies that must remain loadable
   beside the output (MahApps `pack://`, NUnit payload, Nice3point Polyfill
   sidecar).
7. **MCP uses that rule, not a special ILRepack mode.** Copy-local MCP on the
   host merges into the host DLL (no siblings). That ILRepack step removes
   standalone `ModelContextProtocol*` assembly identities from the output
   directory. Toolsets use `ExcludeAssets=runtime` — they do not ship MCP.
   `McpToolsetIsolationPlan` `Pin`s host-loaded MCP contract assemblies
   (kernel share table keyed by simple name). Automatic toolset ALC bind from
   the host load context for repacked MCP is **not implemented** in the
   isolation kernel; toolsets on that path cannot resolve MCP by name without a
   separate strategy (S1 / S2 — open;
   [`2026-09-03-mcp-layer-identity-s5`](../plans/completed/2026-09-03-mcp-layer-identity-s5.md)).
   Do not exclude `ModelContextProtocol*` by name from ILRepack.
8. **Defaults live in `props/ILRepack.targets`; a csproj only opts in or
   overrides.** Driver defaults: `ILRepackable=false`, `ILRepackUnion=true`,
   `ILRepackInternalize=false`, `ILRepackILLink=false`, `ILRepackParallel=true`.
   A csproj may set `ILRepackable=true`, `ILRepackInternalize=true` (nupkg /
   adapter: hide merged types and drop leftover excludes + STJ satellites),
   `RepackBinariesKeep` (TestAdapter: `DevTools.Testing.Abstractions.dll` on
   every TFM so testhost MTP shares `HostTestDiscovery`), and
   `RepackBinariesExcludes`. Do not restate Union, ILLink, Parallel, or
   `Internalize=false`. Rationale stays in this file.

## Alternatives Considered

1. **`ricaun.ILRepack` as the driver.** Same `ILRepack.exe`; different MSBuild
   timing (before `CopyFilesToOutputDirectory`), opt-out enablement, and prefix
   ignores. Would still need `/union` and the Polyfill rules. Rejected as a
   substitute; not a drop-in for NUnit host packaging hooks either.
2. **`/allowdup` instead of `/union`.** ricaun’s Revit sample uses this for
   duplicate types. Untested here; `/union` is what currently packs
   `Microsoft.Extensions.*`.
3. **Disable ILRepack on net10.** Avoids the smashed image but ships a
   sibling-DLL graph and reopens identity conflicts. Rejected.
4. **Keep Polyfill net4-only.** Avoids net10 copies but blocks
   `ThrowIfNull` / `Lock` and other downlevel APIs on net8 in shared code.
   Rejected — all TFMs consume the same source package; merge safety is
   EmbeddedAttribute + one package version + the Nice3point sidecar.
5. **`RepackBinariesExcludes` for `ModelContextProtocol*`.** A second packing
   mode for one package family. If MCP must not be in the host image, it must
   not be copy-local. Rejected.
6. **`PolyfillLib` instead of source `Polyfill` on net48.** A compiled DLL
   would stop embedding `Polyfills.Polyfill` per assembly, but the net48
   package graph pulls `Microsoft.Bcl.Memory`, `System.Memory`,
   `System.Runtime.CompilerServices.Unsafe`, `System.Net.Http`,
   `System.ValueTuple`, and related BCL packages as copy-local merge inputs.
   That reopens ILRepack `/union` identity and isolated-ALC bind cost the
   source package avoids. Rejected — net48 stays on source `Polyfill`.

## Consequences

Positive:

- Isolated ALC (CoreCLR 10) can load a `/union` host image when merged
  `Polyfills.Polyfill` copies share one package version and feature flags.
- Every TFM can call `ArgumentNullException.ThrowIfNull` and other polyfilled
  APIs; net8 / net10 still compile against the BCL where the API already exists.
- Native runtimes and compile-only packages stay out of the merge without
  filename excludes.
- MCP toolsets stay compile-only; the host image carries one in-process MCP
  copy but removes standalone assembly identities needed for name-based ALC bind.

Tradeoffs:

- Nice3point Extensions remains a sidecar on net10 Revit until that package
  ships the same Polyfill version (or none).
- `/union` + a **foreign** Polyfill copy will fail the same way; exclude or
  stop embedding, do not add comments. Do not `Remove` our GlobalPackageReference
  to dodge that — that reintroduces CS0436 / missing ThrowIf* on net48.
- Catalog still uses SDK types to reflect-invoke toolsets, so the host pack
  embeds MCP. [0027](0027-mcp-product-surface.md) owns the pipe DTOs, not
  this merge. Do not add an MCP filename exclude.
- Toolsets with `ExcludeAssets=runtime` on a repacked host need a foreign JSON
  bridge or a packaging strategy (S1 / S2 — open;
  [`2026-09-03-mcp-layer-identity-s5`](../plans/completed/2026-09-03-mcp-layer-identity-s5.md)).

## Follow-Up

- Drop the Nice3point sidecar when that package no longer embeds
  `Polyfills.Polyfill` on net10.
- Revisit `/allowdup` only with pack + isolated-ALC load evidence.
- Choose S1 or S2 at the
  [`2026-09-03-mcp-layer-identity-s5`](../plans/completed/2026-09-03-mcp-layer-identity-s5.md)
  strategy gate before changing toolset packaging or expecting name-based MCP
  bind on a repacked host. Do not paper over with `RepackBinariesExcludes`.

## References

- Driver: [`props/ILRepack.targets`](../../props/ILRepack.targets)
- Build digest: [`docs/agents/build-matrix.md`](../agents/build-matrix.md)
- Host wire (SDK types, no SDK session): [0027](0027-mcp-product-surface.md)
