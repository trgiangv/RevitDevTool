# 0060 Framework Runtime Shims Ship With NetTest

Date: 2026-11-06

## Status

**Proposed 2026-11-06.** Builds on [0059](0059-independent-release-trains-and-one-gate-ci.md)
(trains) and [0055](0055-product-version-wire-contracts-and-satellite-compatibility.md)
(contract ranges). When accepted, it replaces the part of
[0021](0021-testing-kernel-and-provider-owned-framework-runtime.md) that puts
the framework runtime in the add-in payload, and the exact consumer pins of
[0038](0038-mstest-host-provider.md) and [0042](0042-mtp-2-5-removable-test-workarounds.md).
Shipped adapters and add-ins are not carried forward (0059).

## Context

### Where the lock is today

| Piece | Lives in | Framework version |
|-------|----------|-------------------|
| `RevitDevTool.TestAdapter` nupkg | `nettest` | No framework dependency. Declares only `Microsoft.Testing.Platform.MSBuild >= 2.5.2` |
| Testhost siblings `DevTools.{NUnit,TUnit,MSTest}.MTP.dll` | nupkg `build/runtime/{tfm}` | Built against the central pins, run against the user's framework in the user's testhost |
| Runtime shims `DevTools.{NUnit,TUnit,MSTest}.Runtime.dll` | add-in `NUnitRuntime\`, `TUnitRuntime\`, `MSTestRuntime\` | Built against the central pins |
| Framework closure (NUnit, TUnit.Engine, MSTest, MTP) | add-in, beside each shim | Exactly the central pins |

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

### Why a shim fits one framework

The shims reach past the public framework API: `NUnit.Framework.Internal` and
`Internal.Builders` (`NUnitFilterFactory.cs`, `NUnitResultMapper.cs`,
`NUnitAssemblyBuilder.cs`), reflection on the private
`TestContext.DefaultWorkDirectory` (`NUnitAssemblyBuilder.cs:107`), internal
TUnit.Engine and MTP constructors, and MTP internals for MSTest. So a framework
minor can, rarely, break a shim.

That coupling belongs between the shim and the framework, which the user already
controls together in one csproj. It does not belong in the add-in.

## Decision

### 1. Ownership

| Piece | Owner | Ships in |
|-------|-------|----------|
| Kernel: generation, shadow copy, isolation, activation, output routing | `addin` | Add-in payload |
| `DevTools.Testing.Abstractions` (`ITestingRuntimeSession`, `ITestEventSink`, DTOs) | `addin` (host side) | Add-in payload, loaded once per host process |
| Runtime shims `DevTools.{NUnit,TUnit,MSTest}.Runtime.dll` and their manifests | `nettest` | nupkg `build/runtime/{tfm}`, copied to the user's output by `buildTransitive` targets |
| Framework closure | The user | The user's `PackageReference`, in the user's output |

The add-in no longer ships `*Runtime\` folders, any framework assembly, or any
framework version.

### 2. Two contracts, both coarse

| Contract | Between | Checked | Changes when |
|----------|---------|---------|--------------|
| `testing.runtime` range (`Current`/`Min`) | Add-in kernel ↔ shim (`Testing.Abstractions`) | Host, before loading the shim | `Testing.Abstractions` changes |
| Framework **major** | Shim ↔ user's framework | Build, then host | The user moves to another framework major |

Within one add-in major, `testing.runtime` `Min` never rises (0055 §3), so any
adapter of that major runs on any add-in of that major. Within one framework
major, the user moves the framework freely.

### 3. The shim manifest

`DevTools.<Framework>.Runtime.json`, beside the shim, read by the host before it
loads anything:

```json
{
  "framework": "NUnit",
  "entry": "DevTools.NUnit.Runtime.NUnitRuntimeSession",
  "contract": { "current": 1, "min": 1 },
  "assembly": "nunit.framework",
  "version": "5.0.0",
  "netfx": { "binds": ["System.Text.Json"], "omit": [] }
}
```

`version` is the one framework version the shim was built against. Its major is
the supported major, and the full value appears in messages. The build writes it
from the central pin, so it is never edited by hand. `netfx` carries the closure
rules that the add-in payload used to hold; the kernel applies them without knowing
the framework.

### 4. What the host does

1. **No manifest:** "RevitDevTool.TestAdapter <v> is not compatible with
   RevitDevTool <v>. Update RevitDevTool.TestAdapter to the latest version."
2. **`testing.runtime` ranges do not overlap:** the 0055 §4 sentence.
3. **Framework major differs from the major of `version`:** "<Framework> <found> is not supported by
   RevitDevTool.TestAdapter <v> (built for <Framework> <major>.x). Update
   RevitDevTool.TestAdapter to a version for <Framework> <found major>."
4. **Load** the shim and the user's closure in the isolated context. The user's
   framework copy is always the one used.
5. **A bind failure while activating or running** (`MissingMethodException`,
   `MissingFieldException`, `TypeLoadException` from the framework) becomes one
   sentence: "<Framework> <found> changed an API that RevitDevTool.TestAdapter <v>
   uses (built against <version>). Update RevitDevTool.TestAdapter, or use
   <Framework> <version>."

Every stop is `ProtocolIncompatible` with that sentence, not `ProviderFailed` with
a stack trace, and does not poison the session. The adapter checks steps 1 to 3
in the testhost too, so most mismatches end before a host starts.

### 5. NUnit: one copy per host process

NUnit stays host-shared, as today. The first NUnit run in a Revit process loads
the user's copy; a later run with another file version stops with "Revit has NUnit
<loaded> loaded from an earlier run. Restart Revit to run tests with NUnit
<requested>."

### 6. Build check

`buildTransitive` targets fail the build when the resolved framework package is a
different major from the manifest, with the step 3 sentence.

### 7. `Testing.Abstractions` changes by addition only

- Existing members never change or go away. A new capability is a new interface
  (`ITestingRuntimeSession2`), detected with `is`. No default interface members
  (net48).
- `AssemblyVersion` stays `1.0.0.0` while `testing.runtime` `Min = 1`.
- API compatibility against the last released assembly runs in CI
  (`EnablePackageValidation` or `Microsoft.DotNet.ApiCompat`).
- A new capability raises `Current`; a removal raises `Min`, only in a new add-in
  major.

### 8. Moving a framework pin

A normal `nettest` change: move the pin, keep CI green, bump the version, and
write in the changelog which framework versions it was built against and any
internal API the shim now uses differently. It never needs an `addin` release.

### 9. What the user experiences

| Change | User does |
|--------|-----------|
| Add-in update inside a major | Nothing |
| Add-in major that raises `testing.runtime` `Min` | Update the adapter (the sentence says so) |
| Adapter update | Nothing on the add-in |
| Framework minor or patch | Nothing, as a rule |
| Framework minor that breaks the shim (rare) | Update the adapter, or step the framework back (the sentence names both) |
| Framework major | Update the adapter (build error) |
| Two NUnit versions in one Revit session | Restart Revit |

### 10. Rollout

MSTest, then TUnit, then NUnit inside the repo. Users get them in one `addin`
major and one `nettest` release.

## Alternatives Considered

1. **Exact framework versions or a `verified` list in the manifest.** Every
   framework patch would need a `nettest` release.
2. **A verified-pairs index published online.** No package release, but a hosted
   file to maintain and a network dependency for test runs.
3. **Shims on public framework API only.** Would make the major range a vendor
   promise, but it is a rewrite (NUnit `FrameworkController`, MTP in-process),
   larger than the risk it removes.
4. **The add-in carries one shim per framework version.** Every framework release
   becomes an `addin` release.
5. **Keep the pins, add ranges and a clear message.** Better errors, but the add-in
   and the adapter still move together.

## Consequences

Positive:

- The add-in and the adapter never have to update together inside an add-in major.
- Framework minors and patches need nothing from us.
- Every mismatch ends with one sentence that names the fix.
- The add-in payload drops three framework closures.

Tradeoffs:

- A framework minor that changes an internal the shim uses fails at run time, with
  a clear sentence, until the user steps back or a new adapter ships.
- `Testing.Abstractions` is a cross-train ABI with add-only rules.
- Two NUnit versions in one Revit session need a restart.

## Follow-Up

- Check on net48 that a non-strong-named `Testing.Abstractions` with a fixed
  `AssemblyVersion` binds for every shim, and that the isolated context loads a
  shim and the user's closure from the shadow directory with the manifest `netfx`
  rules.
- Plan in `docs/plans/active/`, one framework at a time (§10).
- Update `docs/product/host-testing.md` and `docs/architecture/` testing pages when
  the first framework lands.
