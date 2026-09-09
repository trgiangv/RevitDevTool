# NUnit

Run NUnit `4.6.1` tests inside a live Revit or AutoCAD-family host through Microsoft Testing Platform (MTP) and the `DevTools.TestAdapter` assembly. The adapter is distributed as the public NuGet package `RevitDevTool.TestAdapter` `0.0.7`.

## Project setup

Add the adapter and NUnit to the test project:

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
  <TestingPlatformCommandLineArguments>--report-trx</TestingPlatformCommandLineArguments>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.0.7" />
  <PackageReference Include="NUnit" Version="4.6.1" />
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

Create `global.json` beside the test `.csproj` so this project uses Microsoft Testing Platform:

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

The adapter is MTP-native. Do not add `NUnit3TestAdapter` or configure the project as a VSTest project.

On `net48` targets (host 2024 and older) add `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` — the test project is an executable and NuGet restore cannot take the RID from a package (`NETSDK1047`).

The host properties in the project file select the Autodesk application. `HostName` is `Revit`, `AutoCad`, `Civil3D`, `Plant3D`, `AcadArch`, `AcadMech`, `AcadElec`, `AcadMep`, or `AcadMap3D`. The host year must match the API references used to compile the test assembly.

## Writing tests

Use normal NUnit attributes such as `[TestFixture]`, `[Test]`, `[SetUp]`, and parameterized test cases. Tests execute in host context, so document access, transactions, selection, and UI-thread rules still apply.

```csharp
[TestFixture]
public sealed class WallTests
{
    [Test]
    public void Active_document_is_available()
    {
        Assert.That(Context.Document, Is.Not.Null);
    }
}
```

## Running

```powershell
dotnet test --project .\MyHostTests.csproj -c Debug --filter Active_document
```

Run the command from the test project directory, where `global.json` and the `.csproj` live. Use the adapter's `--filter` option with a method-name substring; use `--list-tests` to inspect discovered tests. Set `<ForceLaunch>true</ForceLaunch>` in the project when a fresh host is required.

## Debugging and troubleshooting

Attach Visual Studio, Rider, or C# Dev Kit to the Autodesk host process before running a test when you need to debug host API code. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

| Symptom | Check |
| --- | --- |
| Zero tests discovered | Confirm MTP packages and NUnit references are present; do not use VSTest adapters |
| Host does not start | Check `hostName`, `hostVersion`, installation path, and `launchTimeout` |
| Test times out | Check modal dialogs, document state, and `perTestTimeout` |
| API call fails | Verify the test is executing inside the expected host and target framework |

See [Testing Overview](/docs/testing/Testing-Overview) for the shared pipe and lifecycle model.
