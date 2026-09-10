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

HostName: `Revit`, `AutoCad`, `Civil3D`, `Plant3D`, `AcadArch`, `AcadMech`,
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

### Targeting net48 (Revit / AutoCAD 2024 and older)

Do not add `<RuntimeIdentifier>` on net8 / net10. The package flattens
testhost output (`AppendRuntimeIdentifierToOutputPath=false`). Restore
cannot read a RID from this package (`NETSDK1047` if the props set one).
On net8 / net10 a project-level RID also nests testhost output under
`win-x64`, which Test Explorer can bind instead of the current build.

### Central `Polyfill`

Host-test projects do not need `Polyfill`. NUnit never uses it. net48 TUnit
gets `[ModuleInitializer]` from this package.

If the repo uses central package management with a `GlobalPackageReference`
named `Polyfill` (typical in Revit add-in trees), **remove it on the test
project** or TUnit restore can duplicate the package (`NU1504`) and a second
declaration can collide (`CS0436`):

```xml
<GlobalPackageReference Remove="Polyfill" />
```

Leave `NetFxModuleInitializer` unset. The package compiles
`ModuleInitializerAttribute` into net4x TUnit projects and already skips when
`Polyfill` or a compile item named `ModuleInitializerAttribute.cs` is present.

Set it to `false` when that skip cannot see your type (otherwise `CS0436`): the
attribute lives in a differently named file, you use PolySharp / a polyfill
package not named `Polyfill`, or you restore `Polyfill` yourself and set
`EnableTUnitPolyfills=true`.

```xml
<NetFxModuleInitializer>false</NetFxModuleInitializer>
```

NUnit and `net8.0-windows` / `net10.0-windows` ignore `NetFxModuleInitializer`.

`dotnet test` needs this in `global.json`. Put it on the **repo root** when
the whole repo's `dotnet test` surface is MTP. One VSTest leftover: keep MTP
at root and put `"runner": "VSTest"` (plus `sdk`) in that project's folder.
Scope MTP next to the test project only when many non-MTP tests share the tree.

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

```powershell
dotnet test --project path/to/Host.Tests/Host.Tests.csproj -c Debug --filter MethodName
```

`--filter` is a test method name or substring. Run `dotnet test` from a
directory covered by that `global.json` (repo root when the runner is there).
A VSTest override is cwd-only: `cd` into that folder before `dotnet test`.
