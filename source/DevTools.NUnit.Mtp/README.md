# RevitDevTool.NUnit

Runs NUnit tests inside a live Revit or AutoCAD-family host. Requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool). Do not also
reference `NUnit3TestAdapter` or `Microsoft.Testing.Platform`.

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
  <OutputType>Exe</OutputType>
  <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
  <HostName>Revit</HostName>
  <HostVersion>$(RevitVersion)</HostVersion>
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

`--filter` is the NUnit method name. Always run `dotnet test` from the
folder that contains that `global.json`.
