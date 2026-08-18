# RevitDevTool.TestAdapter

Microsoft.Testing.Platform adapter that runs tests inside a live Revit or
AutoCAD-family host. Requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool).

The package does not depend on a test framework. Local discovery is PE metadata
scanned for attribute type names you declare. The default in-host engine is
NUnit; override `TestingFramework` and `TestingDiscoveryAttributes` in the test
project to switch without changing this package.

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
  <TestingDiscoveryAttributes>TestAttribute;TestCaseAttribute;TestCaseSourceAttribute;TheoryAttribute</TestingDiscoveryAttributes>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="NUnit" Version="4.6.1" />
</ItemGroup>
```

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

`--filter` is the test method name. Always run `dotnet test` from the
folder that contains that `global.json`.
