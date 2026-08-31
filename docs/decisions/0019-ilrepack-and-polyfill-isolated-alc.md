# 0019 ILRepack And Polyfill On Isolated Load Contexts

Date: 2026-08-16

## Status

Accepted

[0027](0027-mcp-sdk-host-wire-adoption.md) owns the host **wire** (SDK DTOs
allowed; no `McpServer` session on the pipe). This decision owns the **merge**:
one ILRepack pipeline for every opt-in project. MCP is not a second packing
mode. The load context and TFM are the variables.

## Context

Host add-ins merge copy-local managed DLLs into one assembly
(`props/ILRepack.targets`, opt-in `ILRepackable`). The merge uses ILRepack
`/union` because `Microsoft.Extensions.*` packages contribute duplicate types
(for example `LoggingBuilderExtensions`). Without `/union` the pack fails.

`/union` of **two** assemblies that both embed `Polyfills.Polyfill` produces an
image CoreCLR rejects at `AssemblyLoadContext.LoadFromAssemblyPath`:

```text
System.BadImageFormatException: Invalid token
```

That rejection is a **.NET 10 isolated ALC** importer check. The default load
context and .NET Framework are more permissive of the same smashed metadata.
Autodesk host year is only how this repo maps to `net10.0-windows`; it is not
the defect.

Polyfill is a compile-time source package. On TFMs that already have the APIs it
must not become a runtime merge input.

`ricaun.ILRepack` is a wrapper around the same `ILRepack.exe` (2.0.46). It does
not change `/union` semantics and is not a substitute for this policy.

Unmanaged Scintilla/Lexilla satellites are copied under
`runtimes/win-x64/native/` (`DevTools.Logging` direct `Scintilla5.NET` reference
plus `KeepScintillaWinX64Native` in `props/Common.props`). They are not
copy-local at output root, so they are not ILRepack inputs.

MCP follows that same input rule, split by who loads the assembly. .NET
toolsets compile against MCP (`ExcludeAssets=runtime`) and do not ship it:
Catalog reflects into a collectible ALC that binds `ModelContextProtocol*` from
the host load context. Host-loaded projects copy-local MCP so ILRepack embeds
it (no siblings). Do not exclude `ModelContextProtocol*` by filename.

## Decision

1. **Keep the in-repo driver.** `props/ILRepack.targets` + `ILRepackable` remain
   the merge mechanism. The driver adds the `ILRepack` PackageReference when
   that flag is true. Do not switch hosts to `ricaun.ILRepack`.
2. **Polyfill is net4-only.** `GlobalPackageReference` Polyfill is conditioned
   on `$(TargetFramework.StartsWith('net4'))` (repo `Directory.Packages.props`
   and in-repo stub-gen). net8 / net10 must not embed `Polyfills.Polyfill`.
3. **`/union` is a driver default (`ILRepackUnion=true`).** Do not copy it onto
   a csproj. Do not replace it with `/allowdup` without a proven pack **and**
   isolated-ALC load of the host DLL.
4. **ILLink and Parallel stay driver defaults for every TFM.**
   `ILRepackILLink=false` (`/illink` concatenates unused XML; this repo does not
   ILLink the packed add-in) and `ILRepackParallel=true` (speed only). Do not
   flip these per `net10` or restate them on a csproj. Isolated ALC is handled
   by Polyfill net4-only and the Nice3point sidecar. Do not turn `ILRepackable`
   off to dodge isolated ALC.
5. **At most one `Polyfills.Polyfill` in the merge.** Any remaining assembly
   that still embeds it stays beside the add-in (`RepackBinariesExcludes`).
   Today that is `Nice3point.Revit.Extensions.dll` on the Revit host when the
   TFM is net10.
6. **ILRepack only sees copy-local managed DLLs.** What must not merge must not
   land in `TargetDir`. Scintilla/Lexilla live under `runtimes/win-x64/native/`.
   JetBrains.Annotations is compile-only (`PrivateAssets=all`,
   `ExcludeAssets=runtime` in `props/Common.props`). .NET toolsets keep MCP
   compile-only the same way. Host-loaded projects still copy-local MCP so
   ILRepack embeds it and the toolset ALC can bind `ModelContextProtocol*` from
   the host load context. None of these are ILRepack filename excludes.
   `RepackBinariesExcludes` is only for assemblies that must remain loadable
   beside the output (MahApps `pack://`, NUnit payload, Nice3point Polyfill
   sidecar).
7. **MCP uses that rule, not a special ILRepack mode.** Copy-local MCP on the
   host merges into the host DLL (no siblings). Toolsets use
   `ExcludeAssets=runtime` — Catalog reflects, they do not ship MCP. Do not
   exclude `ModelContextProtocol*` by name.
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
4. **Keep Polyfill on all TFMs.** Maximizes net48 convenience and guarantees
   multiple `Polyfills.Polyfill` copies in a net10 merge.
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

- Isolated ALC (CoreCLR 10) can load a `/union` host image if only one Polyfill
  copy is merged.
- net48 still gets Polyfill; modern TFMs use the BCL.
- Native runtimes and compile-only packages stay out of the merge without
  filename excludes.
- MCP toolsets stay compile-only; the host image carries MCP for ALC bind.

Tradeoffs:

- Nice3point Extensions remains a sidecar on net10 Revit until that package
  stops embedding Polyfill.
- `/union` + a future second Polyfill copy will fail the same way; exclude or
  stop embedding, do not add comments.
- Catalog still uses SDK types to reflect-invoke toolsets, so the host pack
  embeds MCP. [0027](0027-mcp-sdk-host-wire-adoption.md) owns the pipe DTOs, not
  this merge. Do not add an MCP filename exclude.

## Follow-Up

- Drop the Nice3point sidecar when that package no longer embeds
  `Polyfills.Polyfill` on net10.
- Revisit `/allowdup` only with pack + isolated-ALC load evidence.
- Toolsets keep MCP `ExcludeAssets=runtime`. Host copy-local MCP stays the ALC
  bind source. Do not paper over with `RepackBinariesExcludes`.

## References

- Driver: [`props/ILRepack.targets`](../../props/ILRepack.targets)
- Build digest: [`docs/agents/build-matrix.md`](../agents/build-matrix.md)
- Host wire (SDK types, no SDK session): [0027](0027-mcp-sdk-host-wire-adoption.md)
