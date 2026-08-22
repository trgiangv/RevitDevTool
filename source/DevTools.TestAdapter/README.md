# RevitDevTool.TestAdapter

Microsoft.Testing.Platform adapter that runs tests inside a live Revit or
AutoCAD-family host. Requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool).

The package does not depend on a test framework. NUnit remains the default and
uses local `ExploreTests` discovery via `DevTools.NUnit.MTP.dll`. Native TUnit
is Revit-only (`HostName=Revit`) and follows the same year → TFM mapping as
the host add-in. `DevTools.TUnit.MTP.dll` reads TUnit.Core
`Sources.TestEntries` locally; in-host execution uses the same catalog through
TestRunner and `testing/run` IPC, not a nested MTP testhost.

Host options come from csproj properties. The package generates `testconfig.json`
and Microsoft.Testing.Platform.MSBuild copies it to `[AssemblyName].testconfig.json`.
The adapter reads the `devtools` section through MTP `IConfiguration`. Do not use
`.runsettings`.

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
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="NUnit" Version="4.6.1" />
</ItemGroup>
```

For TUnit 1.65.38, keep the same host properties and replace the framework
items with:

```xml
<PropertyGroup>
  <TestingFramework>tunit</TestingFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="TUnit" Version="1.65.38" />
</ItemGroup>
```

TUnit requires `HostName=Revit`. It is not gated to specific Revit years;
use the same `HostVersion` / TFM as any other Revit test project.

AutoCAD-family projects set `<HostName>AutoCad</HostName>` or
`Civil3D`, and `<HostVersion>$(AutoCadVersion)</HostVersion>` (or the
year string). Keep a `global.json` next to the test project:

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
