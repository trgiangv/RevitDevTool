# MSTest

Run MSTest tests inside a live Revit or AutoCAD-based host through Microsoft Testing Platform (MTP).

- **Framework:** MSTest `4.5.1` (`MSTest.Sdk`)
- **Adapter:** [`RevitDevTool.TestAdapter`](https://www.nuget.org/packages/RevitDevTool.TestAdapter/0.1.4) `0.1.4`
- **Microsoft.Testing.Platform:** `2.5.1`
- **Lifecycle:** the same as [NUnit](/docs/testing/NUnit); the project SDK and attributes differ

## Project setup

Use `Sdk="MSTest.Sdk"` so the MSTest runner is on:

```xml title="MyHostTests.csproj"
<Project Sdk="MSTest.Sdk/4.5.1">
  <PropertyGroup>
    <TestingFramework>mstest</TestingFramework>
    <HostName>Revit</HostName>
    <HostVersion>2025</HostVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.4" />
    <PackageReference Include="Nice3point.Revit.Toolkit" Version="2025.*" />
  </ItemGroup>
</Project>
```

:::info Why MSTest.Sdk
`Microsoft.NET.Sdk` with `<EnableMSTestRunner>true</EnableMSTestRunner>` is the other accepted shape. Without that runner, MSTest turns Microsoft.Testing.Platform off and no testhost entry point is generated.
:::

Pin `MSTest.Sdk` `4.5.1` on the `Sdk` attribute, or in `global.json`:

```json title="global.json"
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" },
  "msbuild-sdks": { "MSTest.Sdk": "4.5.1" }
}
```

`HostName` and `HostVersion` are required. For the full list of host names and the optional launch and timeout settings, see [Host settings](/docs/testing/Testing-Overview#host-settings).

:::warning Keep versions and packages exact
- The in-host closure is MSTest.TestFramework `4.5.1` plus Microsoft.Testing.Platform `2.5.1`. A different MSTest version fails generation.
- Do not add `NUnit3TestAdapter`, `Microsoft.Testing.Extensions.VSTestBridge` or a `.runsettings` file.
- Do not set `<RuntimeIdentifier>` on net8 or net10.
- On net48, set `win-x64` only when the SDK needs it for an x64 executable.
:::

## Writing tests

Use normal MSTest attributes (`[TestClass]`, `[TestMethod]`, `[TestInitialize]`). Tests run in host context, so document access, transactions and UI-thread rules still apply.

```csharp title="HostTests.cs"
using Nice3point.Revit.Toolkit;

[TestClass]
public sealed class HostTests
{
    [TestMethod]
    public void Revit_application_is_available()
    {
        Assert.IsNotNull(RevitContext.Application);
    }
}
```

:::tip Dependencies run first
Selecting one test also runs its `[DependsOn]` prerequisites (method and class, transitive).
:::

:::tip Use Nice3point.Revit.Toolkit
It brings the Revit API references and `RevitContext` (`Application`, `UiApplication`, `ActiveDocument`, `ActiveView`), and it works well with .NET Revit test projects. See [RevitToolkit](https://github.com/Nice3point/RevitToolkit).
:::

## Running

```powershell title="Terminal"
dotnet test --project .\MyHostTests.csproj -c Debug --filter Revit_application
```

- Run it from a directory covered by the MTP `global.json`.
- `--filter` takes a method name or a substring of one; `--list-tests` shows discovery.
- Standard output of passing tests is shown with `--output Detailed` and `--show-stdout All`.

## Debugging and troubleshooting

Attach Visual Studio, Rider or C# Dev Kit to the Autodesk host process before running a test when you need to debug host API code. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

| Symptom | Check |
| --- | --- |
| Zero tests discovered | `TestingFramework` is `mstest`, the project is `MSTest.Sdk` (or `EnableMSTestRunner` is true), and MTP is the `dotnet test` runner |
| Host does not start | `HostName`, `HostVersion`, installation path and `LaunchTimeout` |
| Test times out | Modal dialogs, document state and `PerTestTimeout` |
| `ChainStep` stays 0 on a single test | The installed host is older than the adapter that closes `[DependsOn]`; redeploy RevitDevTool and restart the host |

See [Testing Overview](/docs/testing/Testing-Overview) for the shared pipe and lifecycle model.
