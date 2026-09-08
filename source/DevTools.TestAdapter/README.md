# RevitDevTool.TestAdapter

Microsoft Testing Platform adapter that runs tests inside a CAD/BIM host.
Requires [RevitDevTool](https://github.com/trgiangv/RevitDevTool).

Currently supports **NUnit** and **TUnit** only. Pin the framework in the
test project — the adapter does not pull either. It depends on
`Microsoft.Testing.Platform.MSBuild` 2.4.0; do not add or override it.
TUnit `1.66.27` and Microsoft.Testing.Platform `2.4.0` are a pair — pin
both.

| Package | Version |
|---------|---------|
| `NUnit` | 4.6.1 |
| `TUnit` | 1.66.27 |
| `Microsoft.Testing.Platform.MSBuild` | 2.4.0 |

NUnit and TUnit both work with every host below. Set `HostName` and
`HostVersion` to the version you run. Include a compile-only host API package
(discovery needs it). Do not copy host API DLLs next to the test output.

`HostName`: `Revit`, `AutoCad`, `Civil3D`, `Plant3D`, `AcadArch`, `AcadMech`,
`AcadElec`, `AcadMep`, `AcadMap3D`.

Default engine is NUnit. Set `TestingFramework` to `tunit` to use TUnit.
Do not use `.runsettings`.

### NUnit (default)

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>180</LaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.0.6" />
  <PackageReference Include="NUnit" Version="4.6.1" />
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

### TUnit

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>180</LaunchTimeout>
  <TestingFramework>tunit</TestingFramework>  <!-- default is nunit so need to set TUnit explicitly -->
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.0.6" />
  <PackageReference Include="TUnit" Version="1.66.27" />
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

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
