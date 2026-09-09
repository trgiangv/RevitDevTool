# TUnit

Run TUnit `1.66.27` tests inside a live Autodesk host through Microsoft Testing Platform and the `DevTools.TestAdapter` assembly. The adapter is distributed as the public NuGet package `RevitDevTool.TestAdapter` `0.0.7`. The host lifecycle is the same as for [NUnit](/docs/testing/NUnit); only the test framework and attributes differ.

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
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.0.7" />
  <PackageReference Include="TUnit" Version="1.66.27" />
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

The repository currently aligns Microsoft Testing Platform packages to `2.4.0`. Do not add VSTest adapters.

On `net48` targets (host 2024 and older) add `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` — the test project is an executable and NuGet restore cannot take the RID from a package (`NETSDK1047`). Nothing else: TUnit's generated infrastructure uses `[ModuleInitializer]`, which .NET Framework does not declare, and the adapter compiles that attribute into the project. It stands down when the project already references `Polyfill` or declares its own `ModuleInitializerAttribute.cs`; `<DevToolsNetFxModuleInitializer>false</DevToolsNetFxModuleInitializer>` turns it off.

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
