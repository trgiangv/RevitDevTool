# DevTools.NUnit

Runs NUnit tests inside a live Revit or AutoCAD-family host. Requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool) (the host-test
controller ships in that installer). Do not also reference
`NUnit3TestAdapter` or `Microsoft.Testing.Platform`.

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
  <HostName>Revit</HostName>
  <HostVersion>$(RevitVersion)</HostVersion>
  <HostLaunch>false</HostLaunch>
  <HostTimeout>60</HostTimeout>
  <HostLaunchTimeout>360</HostLaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="DevTools.NUnit" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="NUnit" />
</ItemGroup>
```

AutoCAD-family projects set `<HostName>Revit</HostName>` (or AutoCad, …)
and `<HostVersion>$(RevitVersion)</HostVersion>`. Keep a `global.json`
next to the test project with `"test": { "runner": "Microsoft.Testing.Platform" }`.

```powershell
cd samples/DevTools.NUnit.SampleTests
dotnet test --project DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host
```
