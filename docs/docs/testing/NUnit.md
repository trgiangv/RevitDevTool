# NUnit

Run NUnit `5.0.0` tests inside a live Revit or AutoCAD-family host through Microsoft Testing Platform (MTP) and the `DevTools.TestAdapter` assembly. The adapter is the public NuGet package `RevitDevTool.TestAdapter` `0.1.2`. NUnit is the default engine. TUnit and MSTest are opt-in: [TUnit](/docs/testing/TUnit), [MSTest](/docs/testing/MSTest).

## Project setup

Add the adapter and NUnit to the test project:

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.2" />
  <PackageReference Include="NUnit" Version="5.0.0" />
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

The adapter is MTP-native. Do not add `NUnit3TestAdapter`, `Microsoft.Testing.Extensions.VSTestBridge`, or a `.runsettings` file.

Do not set `<RuntimeIdentifier>` on net8 / net10. On net48, set `win-x64` only when the SDK requires it for an x64 executable. The package flattens testhost output so a leftover RID does not nest under `win-x64`.

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
