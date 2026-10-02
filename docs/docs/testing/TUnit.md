# TUnit

Run TUnit `1.72.10` tests inside a live Autodesk host through Microsoft Testing Platform and the `DevTools.TestAdapter` assembly. The adapter is the public NuGet package `RevitDevTool.TestAdapter` `0.1.2`. Pin TUnit with Microsoft.Testing.Platform `2.4.1` (the adapter already depends on that MSBuild package; do not override it). The host lifecycle is the same as for [NUnit](/docs/testing/NUnit); only the test framework and attributes differ.

## Project setup

Configure MTP and replace NUnit with TUnit:

```xml
<PropertyGroup>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.2" />
  <PackageReference Include="TUnit" Version="1.72.10" />
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

Create the MTP `global.json` beside the test `.csproj`:

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

Do not add VSTest adapters or a `.runsettings` file. Do not set `<RuntimeIdentifier>` on net8 / net10. On net48, set `win-x64` only when the SDK requires it for an x64 executable.

TUnit's generated infrastructure uses `[ModuleInitializer]`, which .NET Framework does not declare. The adapter compiles that attribute into net4x TUnit projects. It stands down when the project already references `Polyfill` or compiles its own `ModuleInitializerAttribute.cs`. Set `<NetFxModuleInitializer>false</NetFxModuleInitializer>` when that skip cannot see your type (a differently named file, PolySharp, or a polyfill package not named `Polyfill`). NUnit, MSTest, and net8 / net10 ignore the property. If the repo has a central `GlobalPackageReference` named `Polyfill`, remove it on the net48 TUnit project (`NU1504` / `CS0436`).

Use the same project-level host settings as NUnit. `HostName` is `Revit`, `AutoCad`, `Civil3D`, `Plant3D`, `AcadArch`, `AcadMech`, `AcadElec`, `AcadMep`, or `AcadMap3D`.

## Writing tests

Use TUnit's attributes and assertions. Tests still run in the Autodesk host, so keep host API access within the supported execution context and isolate document mutations carefully.

```csharp
public class WallTests
{
    [Test]
    public async Task Active_document_is_available()
    {
        await Assert.That(Context.Document).IsNotNull();
    }
}
```

TUnit's async-first style is useful for tests that coordinate host operations, but it does not remove Revit transaction or UI-thread requirements.

## Running and debugging

```powershell
dotnet test --project .\MyHostTests.csproj -c Debug --filter Active_document
```

Run from the test project directory. Use `--list-tests` to inspect discovery and a method-name substring with `--filter` for a focused run. Attach a .NET debugger to the Autodesk host process before running the test when breakpoints are needed. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

| Symptom | Check |
| --- | --- |
| TUnit tests are not discovered | Confirm `<TestingFramework>tunit</TestingFramework>` and the TUnit package |
| MTP starts but host calls fail | Verify host version, API references, and active document preconditions |
| Test hangs | Look for modal host UI or an unfinished transaction |

See [Testing Overview](/docs/testing/Testing-Overview) for shared lifecycle and pipe details.
