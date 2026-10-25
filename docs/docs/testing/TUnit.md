# TUnit

Run TUnit tests inside a live Revit or AutoCAD-based host through Microsoft Testing Platform (MTP).

- **Framework:** TUnit `1.76.0`
- **Adapter:** [`RevitDevTool.TestAdapter`](https://www.nuget.org/packages/RevitDevTool.TestAdapter/0.1.4) `0.1.4`
- **Lifecycle:** the same as [NUnit](/docs/testing/NUnit); only the framework and attributes differ

:::info Prerequisites
- RevitDevTool installed in the host you test against
- .NET SDK 10, selected by the `global.json` below
:::

## Project setup

Set `TestingFramework` to `tunit` and reference the adapter and TUnit:

```xml title="MyHostTests.csproj"
<PropertyGroup>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.4" />
  <PackageReference Include="TUnit" Version="1.76.0" />
  <PackageReference Include="Nice3point.Revit.Toolkit" Version="2025.*" />
</ItemGroup>
```

Create `global.json` beside the `.csproj` so `dotnet test` uses MTP:

```json title="global.json"
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

Set `HostName` and `HostVersion` as shown. Other options and the full list of host names are in [Host settings](/docs/testing/Testing-Overview#host-settings).

:::warning Keep it MTP-only
- Do not add VSTest adapters or a `.runsettings` file.
- Do not set `<RuntimeIdentifier>` on net8 or net10.
- On net48, set `win-x64` only when the SDK needs it for an x64 executable.
:::

:::info TUnit on .NET Framework (net48)
TUnit uses `[ModuleInitializer]`, which .NET Framework does not declare, so the adapter adds it for you.

- It skips this when the project already has `Polyfill` or its own `ModuleInitializerAttribute.cs`.
- If that check misses your setup, set `<NetFxModuleInitializer>false</NetFxModuleInitializer>`.
:::

## Writing tests

Use TUnit's attributes and assertions. The test runs in the host process, so you can check that directly:

```csharp title="HostSmokeTests.cs"
using Nice3point.Revit.Toolkit;

public class HostSmokeTests
{
    [Test]
    public async Task Revit_application_is_available()
    {
        await Assert.That(RevitContext.Application).IsNotNull();
    }
}
```

:::tip Use Nice3point.Revit.Toolkit
It brings the Revit API references and `RevitContext` (`Application`, `UiApplication`, `ActiveDocument`, `ActiveView`), and it works well with .NET Revit test projects. See [RevitToolkit](https://github.com/Nice3point/RevitToolkit).
:::

## Running and debugging

```powershell title="Terminal"
dotnet test --project .\MyHostTests.csproj -c Debug --filter Revit_application_is_available
```

- Run it from the test project directory.
- `--list-tests` shows discovery; `--filter` takes a method name or a substring of one.
- To debug, attach a .NET debugger to the Autodesk host process first. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Build error asking for `HostName` or `HostVersion` | Set both in the `.csproj` |
| TUnit tests are not discovered | `<TestingFramework>tunit</TestingFramework>` and the TUnit package are present |
| MTP starts but host calls fail | Host year, API references and active document preconditions |
| Test hangs | A modal host dialog or an unfinished transaction |

See [Testing Overview](/docs/testing/Testing-Overview) for the shared lifecycle and pipe details.
