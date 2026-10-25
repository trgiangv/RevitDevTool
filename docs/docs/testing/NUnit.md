# NUnit

Run NUnit tests inside a live Revit or AutoCAD-based host through Microsoft Testing Platform (MTP).

- **Framework:** NUnit `5.0.0`, the default engine
- **Adapter:** [`RevitDevTool.TestAdapter`](https://www.nuget.org/packages/RevitDevTool.TestAdapter/0.1.4) `0.1.4`
- **Other frameworks:** [TUnit](/docs/testing/TUnit) and [MSTest](/docs/testing/MSTest) are opt-in

## Project setup

Add the adapter and NUnit to the test project:

```xml title="MyHostTests.csproj"
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.4" />
  <PackageReference Include="NUnit" Version="5.0.0" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2025.*" />
</ItemGroup>
```

Create `global.json` beside the test `.csproj` so the project uses Microsoft Testing Platform:

```json title="global.json"
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

`HostName` and `HostVersion` are required. For the full list of host names and the optional launch and timeout settings, see [Host settings](/docs/testing/Testing-Overview#host-settings).

:::warning Keep it MTP-only
- The adapter is MTP-native. Do not add `NUnit3TestAdapter`, `Microsoft.Testing.Extensions.VSTestBridge` or a `.runsettings` file.
- Do not set `<RuntimeIdentifier>` on net8 or net10.
- On net48, set `win-x64` only when the SDK needs it for an x64 executable. The package flattens testhost output, so a leftover RID does not nest under `win-x64`.
:::

## Writing tests

Use normal NUnit attributes such as `[TestFixture]`, `[Test]`, `[SetUp]` and parameterized cases. Tests run in host context, so document access, transactions, selection and UI-thread rules still apply.

```csharp title="HostTests.cs"
using Nice3point.Revit.Toolkit;

[TestFixture]
public sealed class HostTests
{
    [Test]
    public void Revit_application_is_available()
    {
        Assert.That(RevitContext.Application, Is.Not.Null);
    }
}
```

:::tip Use Nice3point.Revit.Toolkit
It brings the Revit API references and `RevitContext` (`Application`, `UiApplication`, `ActiveDocument`, `ActiveView`), and it works well with .NET Revit test projects. See [RevitToolkit](https://github.com/Nice3point/RevitToolkit).
:::

## Running

```powershell title="Terminal"
dotnet test --project .\MyHostTests.csproj -c Debug --filter Revit_application
```

- Run it from the test project directory, where `global.json` and the `.csproj` live.
- `--filter` takes a method name or a substring of one.
- `--list-tests` shows the discovered tests.
- Set `<ForceLaunch>true</ForceLaunch>` in the project when you need a fresh host.

## Debugging and troubleshooting

Attach Visual Studio, Rider or C# Dev Kit to the Autodesk host process before running a test when you need to debug host API code. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

| Symptom | Check |
| --- | --- |
| Zero tests discovered | MTP packages and NUnit references are present; no VSTest adapters |
| Host does not start | `HostName`, `HostVersion`, installation path and `LaunchTimeout` |
| Test times out | Modal dialogs, document state and `PerTestTimeout` |
| API call fails | The test runs inside the expected host and target framework |

See [Testing Overview](/docs/testing/Testing-Overview) for the shared pipe and lifecycle model.
