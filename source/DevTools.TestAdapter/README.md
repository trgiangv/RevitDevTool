# RevitDevTool.TestAdapter

Microsoft.Testing.Platform adapter that runs tests inside a live Revit or
AutoCAD-family host. Requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool).

The package does not depend on a test framework. NUnit remains the default and
uses local `ExploreTests` discovery via `DevTools.NUnit.MTP.dll`. TUnit uses
`DevTools.TUnit.MTP.dll` with the same host properties as NUnit (`HostName`,
`HostVersion`, timeouts). `DevTools.TUnit.MTP.dll` reads TUnit.Core
`Sources.TestEntries` locally; in-host execution uses the same catalog through
TestRunner and `testing/run` IPC, not a nested MTP testhost.

## Pinned versions (repo baseline)

| Package | Version |
|---------|---------|
| `NUnit` | 4.6.1 |
| `TUnit` | 1.65.63 |
| `Microsoft.Testing.Platform.MSBuild` | 2.3.3 |

Pin these in consumer test projects. The adapter does not pull framework
packages transitively.

Host options come from csproj properties. The package generates `testconfig.json`
and Microsoft.Testing.Platform.MSBuild copies it to `[AssemblyName].testconfig.json`.
The adapter reads the `devtools` section through MTP `IConfiguration`. Do not use
`.runsettings`.

### NUnit (default)

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
  <OutputType>Exe</OutputType>
  <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
  <!-- required -->
  <HostName>Revit</HostName>
  <HostVersion>$(RevitVersion)</HostVersion>
  <!-- default -->
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>180</LaunchTimeout>
  <TestingFramework>nunit</TestingFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="2.3.3" />
  <PackageReference Include="NUnit" Version="4.6.1" />
</ItemGroup>
```

### TUnit

Set `<TestingFramework>tunit</TestingFramework>` and reference `TUnit` 1.65.63.
Host properties are the same as NUnit.

Revit:

```xml
<PropertyGroup>
  <UseRevit>true</UseRevit>
  <IsTestProject>true</IsTestProject>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Revit</HostName>
  <HostVersion>$(RevitVersion)</HostVersion>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="2.3.3" />
  <PackageReference Include="TUnit" Version="1.65.63" />
</ItemGroup>
```

Civil 3D (same pattern as NUnit Civil 3D samples):

```xml
<PropertyGroup>
  <UseAutoCad>true</UseAutoCad>
  <IsTestProject>true</IsTestProject>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Civil3D</HostName>
  <HostVersion>$(AutoCadVersion)</HostVersion>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="2.3.3" />
  <PackageReference Include="TUnit" Version="1.65.63" />
</ItemGroup>
```

Plain AutoCAD: `<HostName>AutoCad</HostName>` with the same `UseAutoCad` /
`HostVersion` properties.

Keep a `global.json` next to the test project:

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

```powershell
cd path/to/Host.Tests
dotnet test --project Host.Tests.csproj -c Debug --filter MethodName
```

`--filter` is a test method name or substring. Always run `dotnet test`
from the folder that contains that `global.json`.

Samples: `samples/DevTools.NUnit.SampleTests`,
`samples/DevTools.NUnit.Civil3D.SampleTests`, `samples/DevTools.TUnit.SampleTests`,
`samples/DevTools.TUnit.Civil3D.SampleTests`.
