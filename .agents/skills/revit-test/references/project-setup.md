# Project setup

Consumer csproj + `global.json` for `RevitDevTool.TestAdapter`. CLI commands stay
in SKILL.md.

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

Create it **in the test project folder** (beside the `.csproj`). Do **not**
put it at the repo or solution root.

```text
repo/
  global.json                 ← do not put MTP runner here
  tests/
    Host.Tests/
      Host.Tests.csproj
      global.json             ← here
```

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

A root `global.json` with `"runner": "Microsoft.Testing.Platform"` applies
to every `dotnet test` under that tree and breaks non-MTP projects.

Use a .NET 10 SDK. Match `-c` to the consumer configurations (`Debug.R24`,
`Release`, …).

Always `cd` to that project folder before `dotnet test` so the SDK picks
up this `global.json`. Running from the repo root ignores it.

## Conflicting packages

Do not add a second test adapter to the same project:

- `NUnit3TestAdapter`
- `ricaun.RevitTest.TestAdapter`
- `Microsoft.Testing.Extensions.VSTestBridge`

Keep one owner: `RevitDevTool.TestAdapter`.

## Runner install

`%APPDATA%/Autodesk/ApplicationPlugins/RevitDevTool.bundle/Contents/DevTools.TestRunner.exe`

Override with MSBuild `TestingRunnerPath` only when the bundle is not
in the default location. Missing file → `"RevitDevTool is not installed"`.
