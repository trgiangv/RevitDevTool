# Project setup

Consumer `csproj` + `global.json` for
[RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter).
Run commands stay in [SKILL.md](../SKILL.md).

## Required csproj

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.2.0" />
  <PackageReference Include="NUnit" Version="5.0.0" />
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

The adapter does not pull NUnit, TUnit, or MSTest. Reference the baseline in the
[adapter README](../../../../source/DevTools.TestAdapter/README.md) or newer in the same
major. A lower version or another major is an error that names the fix:
[Framework versions](../../../../docs/product/host-testing.md#framework-versions).
`Microsoft.Testing.Platform.MSBuild` is transitive from the adapter.

Do not add `Microsoft.Testing.Platform` as a compile package or override MTP.MSBuild.

### Host properties

| Property | Role |
|----------|------|
| `HostName` | `Revit`, `AutoCad`, `Civil3D`, `Plant3D`, `AcadArch`, `AcadMech`, `AcadElec`, `AcadMep`, `AcadMap3D` |
| `HostVersion` | Year, e.g. `2025` |
| `ForceLaunch` | `false` = reuse matching host, start if none. `true` = always start new |
| `PerTestTimeout` | Per-test budget (seconds). Pipe wait ≈ this × tests in the run |
| `LaunchTimeout` | Seconds to wait for a launched host pipe |
| `TestingFramework` | Default `nunit`. Set `tunit` or `mstest` |
| `NetFxModuleInitializer` | net48 TUnit only. Default on. Set `false` if another polyfill already defines `[ModuleInitializer]` |

### Host API package (compile-only)

Include a compile-only host API package matching `HostVersion` so discovery can
resolve Autodesk types. Do **not** copy host API DLLs into the test output.

Common choices (pick one that matches your repo):

| Host | Example package |
|------|-----------------|
| Revit | `Revit_All_Main_Versions_API_x64`, `Nice3point.Revit.Toolkit` |
| AutoCAD / Civil3D | Product-specific NuGet or internal refs with `IncludeAssets="build; compile"` |

### testconfig.json

The build writes the csproj properties into the test assembly as `devtools.*`
assembly metadata; no `testconfig.json` is generated. Author a sibling
`testconfig.json` for `platformOptions`. A `devtools` key in it overrides that one
csproj value; other keys still come from the csproj. Do not use `.runsettings`.

### Target frameworks

Do not set `<RuntimeIdentifier>` on net8 / net10 test projects (nested
`win-x64` output confuses Test Explorer). The package flattens RID output when
a RID is still present. On net48 only, a project may set `win-x64` when the SDK
requires it for x64 (`NETSDK1047`).

## global.json

`dotnet test` defaults to VSTest. MTP needs:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMinor"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Nearest `global.json` **replaces** the whole file (not a merge). Choice is by
**current directory**, not `--project`.

| Repo layout | Where to put MTP |
|-------------|------------------|
| All `dotnet test` projects are MTP | Root `global.json` |
| One leftover VSTest project | Root MTP + nested `global.json` with `"runner": "VSTest"` (include `sdk`); `cd` there before testing it |
| Many VSTest projects remain | MTP `global.json` next to each MTP test project only |

Use a .NET 10 SDK. Match `-c` to configurations that set `HostVersion` and
`TargetFramework` for your host year.

## Conflicting packages

Do not add a second test adapter to the same project:

- `NUnit3TestAdapter`
- `ricaun.RevitTest.TestAdapter`
- `Microsoft.Testing.Extensions.VSTestBridge`

If the repo has `<GlobalPackageReference Include="Polyfill" />`, remove it on
a net48 TUnit project (`<GlobalPackageReference Remove="Polyfill" />`). NUnit
and MSTest do not need it; net48 TUnit gets `[ModuleInitializer]` from the
adapter package.

## Runner

`DevTools.TestRunner.exe` ships inside the package at `tools/runner/`; the build
writes that path into the test assembly as `devtools.runnerPath`, and the adapter
runs only that copy; no environment variable or `testconfig.json` key overrides it.
A missing file fails with one sentence naming the path and asking for a restore.

The runner is framework-dependent: the machine running `dotnet test` needs the
.NET 10 runtime or later, else `"RevitDevTool.TestAdapter needs the .NET 10 runtime or later."`.

The host add-in still comes from [RevitDevTool releases](https://github.com/trgiangv/RevitDevTool).
