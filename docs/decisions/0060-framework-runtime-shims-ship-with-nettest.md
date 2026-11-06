# 0060 Framework Runtimes Ship With NetTest

Date: 2026-11-06

## Status

**Accepted 2026-11-06**, amended the same day with the measurements from the
plan's P0. Builds on [0059](0059-independent-release-trains-and-one-gate-ci.md)
(trains) and [0055](0055-product-version-wire-contracts-and-satellite-compatibility.md)
(contract ranges). It replaces the part of
[0021](0021-testing-kernel-and-provider-owned-framework-runtime.md) that puts
the framework runtime in the add-in payload, and the exact consumer pins of
[0038](0038-mstest-host-provider.md) and [0042](0042-mtp-2-5-removable-test-workarounds.md).
Shipped adapters and add-ins are not carried forward (0059).

## Goal

A framework update with no breaking change needs no add-in update and no adapter
update. "Breaking" includes the framework internals the framework runtimes use, not only the
public API. The versions pinned when this ADR lands are the baseline: users may be
on those versions or higher within the major, never lower.

## Context

### Where the lock is today

| Piece | Lives in | Framework version |
|-------|----------|-------------------|
| `RevitDevTool.TestAdapter` nupkg | `nettest` | No framework dependency. Declares only `Microsoft.Testing.Platform.MSBuild >= 2.5.2` |
| Testhost siblings `DevTools.{NUnit,TUnit,MSTest}.MTP.dll` | nupkg `build/runtime/{tfm}` | Built against the central pins, run against the user's framework in the user's testhost |
| Framework runtimes `DevTools.{NUnit,TUnit,MSTest}.Runtime.dll` | add-in `NUnitRuntime\`, `TUnitRuntime\`, `MSTestRuntime\` | Built against the central pins |
| Framework closure (NUnit, TUnit.Engine, MSTest, MTP) | add-in, beside each framework runtime | Exactly the central pins |

Pins: NUnit 5.0.0, TUnit 1.78.0, MTP 2.5.2 (`Directory.Packages.props`), MSTest.Sdk
4.5.2 (`global.json`).

Before each generation the host compares the user's framework files with these
pins by equality (`TestingGenerationPins.cs:35`, `if (version != expected)`;
`NUnitGenerationPolicy.cs:15`, `ExpectedNUnitFileVersion = "5.0.0.0"`; the TUnit
and MSTest policies do the same). A mismatch throws `TestingGenerationBuildException`,
which the handler returns as `ProviderFailed` with the full exception text.

So an add-in release that moves a pin breaks every test project until the user
updates the framework **and** the adapter, and the error does not say which.
The add-in and the adapter must always move together.

### Why a framework runtime fits one framework

The framework runtimes reach past the public framework API: `NUnit.Framework.Internal` and
`Internal.Builders` (`NUnitFilterFactory.cs`, `NUnitResultMapper.cs`,
`NUnitAssemblyBuilder.cs`), reflection on the private
`TestContext.DefaultWorkDirectory` (`NUnitAssemblyBuilder.cs:107`), internal
TUnit.Engine and MTP constructors, and MTP internals for MSTest. So a framework
minor can break a framework runtime. One such break is on record: the internal
`ClientInfoService` constructor changed in MTP 2.4.0 (commit `b352f80b`).

That coupling belongs between the framework runtime and the framework, which the user already
controls together in one csproj. It does not belong in the add-in.

### What binding allows

Measured in the plan's P0, with real kernel code outside a host:

- Assembly versions of these frameworks change on almost every minor and patch, and
  all of them are strong-named, so the loader's version rule decides everything.
- The kernel requires the exact version on .NET (`AssemblyIdentityMatcher.cs:28`,
  `ManifestAssemblySource.cs:45`, `AssemblyIsolationContext.cs:98`). net48 already
  allows a newer file through `NetfxClosureBind`. `Pin` rejects any difference.
- CoreCLR itself accepts both a higher and a lower version from `ALC.Load`, so the
  floor in §2 is our rule, not the runtime's.
- A framework runtime built at a pin loads and JITs cleanly on newer closures in the probes run:
  TUnit 1.72 on 1.81, MSTest 4.4.1 on 4.5.2. Nothing newer than the pin exists yet
  for NUnit 5 or MSTest 4.
- A lower version in the same major can really lack what a framework runtime uses: the current
  MSTest framework runtime does not compile against 4.4.x.

## Decision

### 1. Ownership

| Piece | Owner | Ships in |
|-------|-------|----------|
| Kernel: generation, shadow copy, isolation, activation, output routing | `addin` | Add-in payload |
| `DevTools.Testing.Abstractions` (`ITestingRuntimeSession`, `ITestEventSink`, DTOs, shared helpers) | `addin` (host side) | Add-in payload, loaded once per host process |
| Framework runtimes `DevTools.{NUnit,TUnit,MSTest}.Runtime.dll` and their manifests | `nettest` | nupkg `build/runtime/{tfm}`, copied to the user's output by the package's `build/` targets |
| A framework runtime's own dependencies that are not part of the framework | `nettest` | Inside the framework runtime (§6), or in-box on .NET |
| Framework closure | The user | The user's `PackageReference`, in the user's output |

The add-in no longer ships `*Runtime\` folders, any framework assembly, or any
framework version.

### 2. Two contracts, both coarse

| Contract | Between | Checked | Changes when |
|----------|---------|---------|--------------|
| `testing.runtime` range (`Current`/`Min`) | Add-in kernel ↔ framework runtime (`Testing.Abstractions` and the entry constructor) | Host, before loading the framework runtime. Runner, on `testing/hello` | That surface changes |
| Framework version: same major as `version`, and not lower than it | Framework runtime ↔ user's framework | Build, then host | The user moves the framework |

Within one add-in major, `testing.runtime` `Min` never rises (0055 §3), so any
adapter of that major runs on any add-in of that major. Upward within one framework
major, the user moves the framework freely.

### 3. The framework runtime manifest

`DevTools.<Framework>.Runtime.json`, beside the framework runtime, read by the host before it
loads anything:

```json
{
  "framework": "NUnit",
  "entry": "DevTools.NUnit.Runtime.NUnitRuntimeSession",
  "contract": { "current": 1, "min": 1 },
  "assembly": "nunit.framework",
  "version": "5.0.0.0"
}
```

`version` is the `AssemblyVersion` of `assembly` in the closure the framework runtime was built
against, read from that file by the build, so it is never edited by hand. It is the
floor, its major is the supported major, and it appears in messages. Assembly
versions are compared with assembly versions because that is what binding uses:
file versions of MSTest and MTP do not follow the package, and an NUnit patch may
keep the assembly version of the previous one.

### 4. What the host does

1. **No manifest:** "RevitDevTool.TestAdapter <v> is not compatible with
   RevitDevTool <v>. Update RevitDevTool.TestAdapter to the latest version."
2. **`testing.runtime` ranges do not overlap:** the 0055 §4 sentence.
3. **Framework major differs from the major of `version`:** "<Framework> <found> is not supported by
   RevitDevTool.TestAdapter <v> (built for <Framework> <major>.x). Update
   RevitDevTool.TestAdapter to a version for <Framework> <found major>."
4. **Framework lower than `version`:** "<Framework> <found> is older than the
   <version> that RevitDevTool.TestAdapter <v> was built against. Update <Framework>
   to <version> or later."
5. **Load** the framework runtime and the user's closure in the isolated context. The user's
   framework copy is always the one used, and a request for a lower version binds it.
   Only testing sessions use this rule; C# script and MCP toolset isolation keep
   theirs.
6. **A bind failure while activating or running** becomes one sentence: "<Framework>
   <found> changed an API that RevitDevTool.TestAdapter <v> uses (built against
   <version>). Update RevitDevTool.TestAdapter, or use <Framework> <version>."
   A bind failure is `MissingMethodException`, `MissingFieldException`,
   `MissingMemberException` (what `InternalMembers` throws), `TypeLoadException`,
   `FileNotFoundException`, or `FileLoadException`, after unwrapping
   `TargetInvocationException` and `TypeInitializationException`, and it must name a
   framework or platform assembly. A failure from the user's own test code is a test
   result and never reaches this rule.

Every stop is `ProtocolIncompatible` with that sentence, not `ProviderFailed` with
a stack trace, and does not poison the session.

The adapter has no check of its own in the testhost. The build check (§7) and the
host already cover steps 1, 3, and 4, and the host range is only known once a host
answers. The runner checks `testing.runtime` on `testing/hello`, so an add-in older
than the framework runtime is explained by the newer side (0055 §10).

### 5. NUnit: one copy per host process

NUnit stays host-shared, as today. The first NUnit run in a Revit process loads
the user's copy; a later run with another file version stops with "Revit has NUnit
<loaded> loaded from an earlier run. Restart Revit to run tests with NUnit
<requested>." The framework runtime binds that shared copy with `Share`, not `Pin`, because the
version normally differs from what it was built against.

### 6. A framework runtime's own dependencies

The user's output holds only the user's closure, so a framework runtime must not need anything
else from it.

- **.NET:** use what is in-box. The NUnit framework runtime takes `System.Reflection.Metadata` from
  the shared framework, not from a package.
- **net48, when a dependency's types stay inside the framework runtime:** merge it with ILRepack
  and internalize. The NUnit framework runtime merges `System.Reflection.Metadata`,
  `System.Collections.Immutable`, `System.Memory`, `System.Buffers`, and
  `System.Numerics.Vectors`.
- **When a dependency's types cross to the framework:** never merge it, because that
  splits type identity. Reference the lowest version that compiles, so the user's
  copy satisfies it (`Microsoft.Bcl.AsyncInterfaces` in the TUnit framework runtime on net48).

NuGet dependencies for this are refused: one package serves all three frameworks, so
every user would receive them. `TestingFramework=tunit` requires the `TUnit` package,
because `TUnit.Engine` comes from it. A project with only `TUnit.Core` fails the build
with one sentence.

### 7. Build check

The package's `build/` targets fail the build when the resolved framework package has
a different major from the manifest (the step 3 sentence), or a lower version (the
step 4 sentence). They read `NuGetPackageVersion` on `@(ReferencePath)`, which works
on all three TFMs and under `MSTest.Sdk`.

### 8. `Testing.Abstractions` changes by addition only

- Existing members never change or go away. This covers the shared helpers in the
  assembly (`InternalMembers`, `CancellableRuntimeSession`), not only the interfaces.
- The constructor signatures the host calls on `entry` are part of this contract.
- `AssemblyVersion` is set in the project and stays `1.0.0.0` while `testing.runtime`
  `Min = 1`. A build must not stamp it from the product version: on .NET, `Pin`
  rejects a framework runtime whose copy differs.
- A new capability is a new interface, with no default interface members (net48). The
  host detects it with `is`. A framework runtime must keep its `entry` type on the old interface,
  because a type that implements an interface the host lacks throws `TypeLoadException`.
  It puts the capability in a separate type, created in a separate non-inlined method
  after probing for the interface by name.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` checks the public surface on every build
  against `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` in the project, so a
  change shows as a text diff and no assembly is kept in git. An undeclared symbol
  (RS0016) or a declared one that no longer exists (RS0017) fails the build on all
  three TFMs. New symbols go in `PublicAPI.Unshipped.txt` and move to
  `PublicAPI.Shipped.txt` at a release. Known limit: someone can delete a line to
  pass the check, so a removal is caught in review, not by the tool.
- A new capability raises `Current`; a removal raises `Min`, only in a new add-in
  major.

### 9. Moving a framework pin

A normal `nettest` change, and it never needs an `addin` release. Moving a pin raises
the floor for every user of that adapter, so move it only when a framework runtime must change.
When it moves: keep CI green, bump the version, and write in the changelog which
framework versions it was built against and any internal API the framework runtime now uses
differently.

### 10. What the user experiences

| Change | User does |
|--------|-----------|
| Add-in update inside a major | Nothing |
| Add-in major that raises `testing.runtime` `Min` | Update the adapter (the sentence says so) |
| Adapter update | Nothing on the add-in, unless the adapter raised the floor above the user's framework (the sentence says so) |
| Framework update with no break, including its internals | Nothing |
| Framework minor that breaks the framework runtime | Update the adapter, or step the framework back (the sentence names both) |
| Framework lower than the baseline | Update the framework (build error) |
| Framework major | Update the adapter (build error) |
| Two NUnit versions in one Revit session | Restart Revit |

### 11. Rollout

MSTest, then TUnit, then NUnit inside the repo. Users get them in one `addin`
major and one `nettest` release.

## Alternatives Considered

1. **Exact framework versions or a `verified` list in the manifest.** Every
   framework patch would need a `nettest` release.
2. **A verified-pairs index published online.** No package release, but a hosted
   file to maintain and a network dependency for test runs.
3. **Framework runtimes on public framework API only.** Would make the major range a vendor
   promise, but it is a rewrite (NUnit `FrameworkController`, MTP in-process),
   larger than the risk it removes.
4. **The add-in carries one framework runtime per framework version.** Every framework release
   becomes an `addin` release.
5. **Keep the pins, add ranges and a clear message.** Better errors, but the add-in
   and the adapter still move together.
6. **Accept lower versions in the same major.** The runtime would load them, but a
   lower version can lack what the framework runtime uses, and the failure comes late.
7. **NuGet dependencies for the framework runtime's own dependencies on net48.** See §6.
8. **A check in the adapter's testhost.** Duplicates the build check and the host, and
   cannot know the host's range before a host answers.

## Consequences

Positive:

- The add-in and the adapter never have to update together inside an add-in major.
- A framework update with no break needs nothing from us.
- Every mismatch ends with one sentence that names the fix.
- The add-in payload drops three framework closures.

Tradeoffs:

- A framework minor that changes an internal the framework runtime uses fails at run time, with
  a clear sentence, until the user steps back or a new adapter ships.
- `Testing.Abstractions` and the `entry` constructors are a cross-train ABI with
  add-only rules.
- Two NUnit versions in one Revit session need a restart.
- Moving a pin raises the floor, so it can force users to update their framework.
- The kernel gains a second version rule, for testing sessions only.
- The NUnit framework runtime grows from about 0.4 MB to about 1.4 MB on net48.

## Follow-Up

- Progress, measurements, and live verification: [the plan](../plans/active/2026-11-06-framework-shims-nettest-0060.md).
- Update `docs/product/host-testing.md` and `docs/architecture/` testing pages when
  the first framework lands.
