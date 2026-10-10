# MSTest

Run MSTest `4.5.1` tests inside a live Revit or AutoCAD-family host through Microsoft Testing Platform (MTP) and the `DevTools.TestAdapter` assembly. The adapter is the public NuGet package `RevitDevTool.TestAdapter` `0.1.3`. The host lifecycle is the same as for [NUnit](/docs/testing/NUnit); the project SDK and attributes differ.

## Project setup

Use `Sdk="MSTest.Sdk"` so `EnableMSTestRunner` is on. `Microsoft.NET.Sdk` with `<EnableMSTestRunner>true</EnableMSTestRunner>` is the other accepted shape. Without that runner, MSTest turns Microsoft.Testing.Platform off and no testhost entry point is generated.

```xml
<Project Sdk="MSTest.Sdk/4.5.1">
  <PropertyGroup>
    <TestingFramework>mstest</TestingFramework>
    <HostName>Revit</HostName>
    <HostVersion>2025</HostVersion>
    <ForceLaunch>false</ForceLaunch>
    <PerTestTimeout>60</PerTestTimeout>
    <LaunchTimeout>360</LaunchTimeout>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.3" />
    <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
      IncludeAssets="build; compile" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

Pin `MSTest.Sdk` `4.5.1` on the `Sdk` attribute, or in `global.json`:

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" },
  "msbuild-sdks": { "MSTest.Sdk": "4.5.1" }
}
```

The in-host closure is MSTest.TestFramework 4.5.1 plus Microsoft.Testing.Platform 2.5.1. A different MSTest version fails generation. Do not add `NUnit3TestAdapter`, `Microsoft.Testing.Extensions.VSTestBridge`, or a `.runsettings` file.

Do not set `<RuntimeIdentifier>` on net8 / net10. On net48, set `win-x64` only when the SDK requires it for an x64 executable. `HostName` is `Revit`, `AutoCad`, `Civil3D`, `Plant3D`, `AcadArch`, `AcadMech`, `AcadElec`, `AcadMep`, or `AcadMap3D`.

## Writing tests

Use normal MSTest attributes (`[TestClass]`, `[TestMethod]`, `[TestInitialize]`). Tests execute in host context, so document access, transactions, and UI-thread rules still apply.

Selecting one test also runs its `[DependsOn]` prerequisites (method and class, transitive).

```csharp
[TestClass]
public sealed class WallTests
{
    [TestMethod]
    public void Active_document_is_available()
    {
        Assert.IsNotNull(Context.Document);
    }
}
```

## Running

```powershell
dotnet test --project .\MyHostTests.csproj -c Debug --filter Active_document
```

Run the command from a directory covered by the MTP `global.json`. `--filter` is a method name or substring. Use `--list-tests` to inspect discovered tests. The package shows standard output for passing tests (`--output Detailed`, `--show-stdout All`).

## Debugging and troubleshooting

Attach Visual Studio, Rider, or C# Dev Kit to the Autodesk host process before running a test when you need to debug host API code. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

| Symptom | Check |
| --- | --- |
| Zero tests discovered | Confirm `TestingFramework` is `mstest`, the project is `MSTest.Sdk` (or `EnableMSTestRunner` is true), and MTP is the `dotnet test` runner |
| Host does not start | Check `HostName`, `HostVersion`, installation path, and `LaunchTimeout` |
| Test times out | Check modal dialogs, document state, and `PerTestTimeout` |
| `ChainStep` stays 0 on a single test | The installed host is older than the adapter that closes `[DependsOn]`; redeploy RevitDevTool and restart the host |

See [Testing Overview](/docs/testing/Testing-Overview) for the shared pipe and lifecycle model.
