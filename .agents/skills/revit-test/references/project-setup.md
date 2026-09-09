# Project setup

Consumer csproj + `global.json` for `RevitDevTool.TestAdapter`. CLI commands stay
in SKILL.md.

## Required csproj

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" />
  <PackageReference Include="NUnit" Version="4.6.1" />
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

Pin **NUnit 4.6.1** (`nunit.framework` file version `4.6.1.0`). The host
generation snapshot rejects a missing or mismatched framework DLL.

The adapter package depends on `Microsoft.Testing.Platform.MSBuild` 2.4.0. Do not add
`Microsoft.Testing.Platform` as a compile package. Do not override MTP.MSBuild.

| Property | Role |
|----------|------|
| `HostName` | `Revit`, `AutoCad`, `Civil3D`, `Plant3D`, `AcadArch`, `AcadMech`, `AcadElec`, `AcadMep`, `AcadMap3D` |
| `HostVersion` | Year, e.g. `2025` |
| `ForceLaunch` | `false` = reuse a matching host, start if none. `true` = always start a new host |
| `PerTestTimeout` | Per-test budget (seconds). The `testing/run` pipe wait is this × tests in the run. 60 is smoke-only |
| `LaunchTimeout` | Seconds to wait for a launched host pipe |
| `TestingFramework` | Default `nunit`. Override in the test csproj to change the in-host engine without changing the package |
| `RuntimeIdentifier` | `win-x64` on net48 (host 2024 and older). Restore does not take a RID from the package (`NETSDK1047`). Keep it if the same csproj also builds 2022–2024 |
| `NetFxModuleInitializer` | net48 TUnit only. Default on: inject `[ModuleInitializer]`. Leave unset when the project already has `Polyfill` or `ModuleInitializerAttribute.cs`. Set `false` when the attribute lives in a differently named file, PolySharp, or a polyfill package not named `Polyfill` (`CS0436` otherwise) |

`HostName` / `HostVersion` are the runner contract. Include a compile-only
host API package (`Revit_All_Main_Versions_API_x64` for Revit) matching that
year so testhost discovery can resolve Autodesk types. Do not copy host API
DLLs into the test output.

Build generates `testconfig.json` from the csproj properties. A normal
incremental `dotnet build` (not only Rebuild) refreshes
`[AssemblyName].testconfig.json`. Microsoft.Testing.Platform.MSBuild also
copies that file. The adapter reads the `devtools` section through
`IConfiguration`. Author `testconfig.json` beside the `.csproj`
to add `platformOptions` (the `devtools` section is merged from csproj unless
you already wrote one). Do not use `.runsettings`. Do not edit the copied
output file by hand.

## global.json

`dotnet test` defaults to VSTest. Add the MTP runner to `global.json`.
Nearest `global.json` replaces the whole file (not a merge) and is chosen
from the **current directory**, not from `--project`.

**All-MTP repo** — every `dotnet test` project uses MTP. Put the runner on the
**root** `global.json`. `dotnet test` from the repo root is correct.

```text
repo/
  global.json                 ← sdk + test.runner MTP
  csharp/Host.Tests/
    Host.Tests.csproj
```

**One VSTest leftover** — same as all-MTP at root, plus a nested override.
Include `sdk` in the nested file. `cd` into that folder before `dotnet test`.

```text
repo/
  global.json                 ← sdk + test.runner MTP
  samples/ricaun.NUnit.SampleTests/
    global.json               ← sdk + "runner": "VSTest"
```

**Many VSTest projects** — do **not** put MTP on the root; scope it next to
each MTP test project.

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

Use a .NET 10 SDK. Match `-c` to the consumer configurations (`Debug.R24`,
`Release`, …). Run `dotnet test` from a directory covered by the intended
runner.

## Conflicting packages

Do not add a second test adapter to the same project:

- `NUnit3TestAdapter`
- `ricaun.RevitTest.TestAdapter`
- `Microsoft.Testing.Extensions.VSTestBridge`

Keep one owner: `RevitDevTool.TestAdapter`.

If `Directory.Packages.props` (or `Directory.Build.props`) has
`<GlobalPackageReference Include="Polyfill" />`, remove it on the host-test
project. The testhost is a single-runtime Exe; NUnit does not need Polyfill,
and net48 TUnit gets `[ModuleInitializer]` from the package.

```xml
<GlobalPackageReference Remove="Polyfill" />
```

## Runner install

`%APPDATA%/Autodesk/ApplicationPlugins/RevitDevTool.bundle/Contents/DevTools.TestRunner.exe`

Override with MSBuild `TestingRunnerPath` only when the bundle is not
in the default location. Missing file → `"RevitDevTool is not installed"`.
