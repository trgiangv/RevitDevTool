# Project setup

Consumer csproj + `global.json` for `RevitDevTool.NUnit`. CLI commands stay
in SKILL.md.

## Required csproj

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
  <OutputType>Exe</OutputType>
  <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
  <HostName>Revit</HostName>
  <HostVersion>2024</HostVersion>
  <HostLaunch>false</HostLaunch>
  <HostTimeout>60</HostTimeout>
  <HostLaunchTimeout>360</HostLaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.NUnit" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="NUnit" Version="4.6.1" />
</ItemGroup>
```

Pin **NUnit 4.6.1** (`nunit.framework` file version `4.6.1.0`). The host
generation snapshot rejects a missing or mismatched framework DLL.

Reference `Microsoft.Testing.Platform.MSBuild`, not
`Microsoft.Testing.Platform` as a compile package.

| Property | Role |
|----------|------|
| `OutputType` | Must be `Exe`. MTP is a test application. Revit/CAD `Directory.Build` often resets `Library` — set this in the test csproj so it wins. Package props also set `Exe`; consumer SDK still overrides unless the csproj repeats it |
| `RuntimeIdentifiers` | Must be `win-x64`. Autodesk `PlatformTarget=x64` infers that RID; without this, restore/`dotnet test` cannot find the exe |
| `HostName` | `Revit`, `AutoCad`, `Civil3D`, … |
| `HostVersion` | Year string (`2024`, `2026`). May be `$(RevitVersion)` if the project already defines it |
| `HostLaunch` | `false` = reuse a matching host, start if none. `true` = always start a new host |
| `HostTimeout` | Whole `nunit/run` pipe timeout (seconds). Raise for large suites; 60 is smoke-only |
| `HostLaunchTimeout` | Seconds to wait for a launched host |

`HostName` / `HostVersion` are the runner contract. Do not invent other
MSBuild flags for the runner.

Build writes `devtools.nunit.host.json` beside the test exe. Do not edit it
by hand.

On **net48**, package targets ILRepack the test exe and delete merged DLLs
(`System.Text.Json`, product libraries, MTP, …). Keep `nunit.framework`
4.6.1 loose — do not merge it. Do not add a separate `System.Text.Json`
PackageReference to “fix” host `FileNotFoundException`. Skip merge with
`<DevToolsNUnitRepack>false</DevToolsNUnitRepack>`. Extra exclude names:
`DevToolsNUnitRepackBinariesExcludes` (semicolon-separated file names).
This is not add-in `IsRepackable`.

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

Package targets fail the build if the same project also references:

- `NUnit.Microsoft.Testing.Platform`
- `NUnit3TestAdapter`
- `ricaun.RevitTest.TestAdapter`
- `Microsoft.Testing.Extensions.VSTestBridge`

Keep one owner: `RevitDevTool.NUnit`.

## Runner install

`%APPDATA%/Autodesk/ApplicationPlugins/RevitDevTool.bundle/Contents/DevTools.NUnit.Runner.exe`

Override with MSBuild `DevToolsNUnitRunnerPath` only when the bundle is not
in the default location. Missing file → `"RevitDevTool is not installed"`.
