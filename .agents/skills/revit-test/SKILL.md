---
name: revit-test
description: >
  Configure, write, and run NUnit, TUnit, or MSTest tests inside Revit, AutoCAD,
  Civil 3D, or a family document with RevitDevTool.TestAdapter (Microsoft Testing
  Platform). Use in a repo that references that package: project setup
  (HostName, HostVersion, ForceLaunch), dotnet test filters and [Explicit] tests,
  or MTP exit code 8 and zero tests discovered.
---

# Host tests (RevitDevTool.TestAdapter)

For any repo that references
[RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter).
To use it elsewhere, copy this folder to `.agents/skills/revit-test/` in that repo
or to your agent's user-level skills folder.

Prerequisites: [RevitDevTool](https://github.com/trgiangv/RevitDevTool) installed
on the machine (it provides the host add-in), and the .NET 10 runtime or later
(the package's `DevTools.TestRunner` is framework-dependent).

```text
dotnet test → MTP testhost (discover locally, no host)
           → DevTools.TestRunner → host pipe → NUnit | TUnit | MSTest in host
```

Test bodies run in the host, never in the MTP process.

## Detect

The consumer project has:

- a `PackageReference` to `RevitDevTool.TestAdapter`;
- a nearest `global.json` (from the directory where you run `dotnet test`) with
  `"test": { "runner": "Microsoft.Testing.Platform" }`.

It is the only test adapter in the project; `NUnit3TestAdapter`,
`ricaun.RevitTest.TestAdapter`, and `Microsoft.Testing.Extensions.VSTestBridge`
conflict with it.

## Quick start

NUnit is the default. TUnit: `TestingFramework=tunit` plus the `TUnit` package.
MSTest: `Sdk="MSTest.Sdk"` plus `TestingFramework=mstest`. Any framework version
at or above the adapter's baseline in the same major runs.

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.3.0" />
  <PackageReference Include="NUnit" Version="5.0.0" />
  <!-- compile-only host API — pick a package that matches HostVersion -->
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

All properties, the `global.json` to copy and where it goes, conflicts, and the
runner: [project-setup.md](references/project-setup.md).

## Run

Run `dotnet test` from a directory covered by the intended `global.json`. The
adapter starts or reuses the host; you do not start `Revit.exe` or `acad.exe`.

```powershell
dotnet test --project path/to/Host.Tests.csproj -c <Config> --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
```

| Behavior | Detail |
|----------|--------|
| Discovery | `--list-tests` and Test Explorer refresh start no host |
| Run | Starts or reuses a host (`ForceLaunch=false` still launches if none match) |
| Passed stdout | `--output Detailed` shows Console, Trace, and `Assert.Pass` on passes; the default `Normal` expands failures only |
| `--filter` | Method-name regex (NUnit `<name re="1">`); VSTest `FullyQualifiedName~` matches nothing |
| `[Explicit]` | Selected only when `--filter` matches the method name |

Filters, `--filter-uid`, and exit 8: [mtp-filter.md](references/mtp-filter.md).

## Write tests

Bodies run on the Autodesk API context. WPF `Dispatcher.Invoke` is not an API
context. Locate fixtures and outputs with `[CallerFilePath]`: test assemblies are
stream-loaded, and NUnit's `TestContext.WorkDirectory` points at a generation
shadow.

| Topic | Reference |
|-------|-----------|
| Shared (paths, smoke, stdout) | [test-patterns.md](references/test-patterns.md) |
| NUnit (default) | [nunit.md](references/nunit.md) |
| TUnit (`TestingFramework=tunit`) | [tunit.md](references/tunit.md) |
| MSTest (`TestingFramework=mstest`) | [mstest.md](references/mstest.md) |

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `--filter FullyQualifiedName~…` or `Name=` matches nothing | Use `--filter MethodName` |
| No passed stdout under `dotnet test` | Add `--output Detailed` |
| `[Explicit]` never runs | Select it with `--filter MethodName` |
| Exit 8, zero tests | Wrong filter syntax, or the cwd `global.json` still selects VSTest |
| `"did not find the TestRunner at '…'"` | Restore the RevitDevTool.TestAdapter package again |
| `"needs the .NET 10 runtime or later"` | Install the .NET 10 runtime or later on the `dotnet test` machine |
| Timeout | Raise `PerTestTimeout` (60 s is for smoke tests) |
| Stale host state (net48) | Restart the host process |

## Links

- NuGet: [RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter)
- Installer and runner: [RevitDevTool](https://github.com/trgiangv/RevitDevTool)
