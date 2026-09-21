---
name: revit-test
description: >
  Configure and run in-host CAD/BIM tests with RevitDevTool.TestAdapter (Microsoft
  Testing Platform). Use when a repo references that NuGet package; writing or running
  NUnit or TUnit inside Revit, AutoCAD, Civil3D, or family; setting HostName,
  HostVersion, or ForceLaunch; using dotnet test --filter; selecting [Explicit]
  tests; or diagnosing MTP exit code 8 / zero tests discovered.
---

# Host tests (RevitDevTool.TestAdapter)

Consumer skill for any repo that references
[RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter).
Copy this folder to `~/.cursor/skills/revit-test/`, `~/.agents/skills/revit-test/`,
or `.agents/skills/revit-test/` in the repo.

**Prerequisites:** [RevitDevTool](https://github.com/trgiangv/RevitDevTool) installed
on the machine (provides `DevTools.TestRunner` and the host add-in).

```text
dotnet test → MTP testhost (discover locally, no host)
           → DevTools.TestRunner → host pipe → NUnit | TUnit in host
```

Test bodies never run in the MTP process.

## Detect

The consumer project should have:

- `PackageReference` `RevitDevTool.TestAdapter`
- Nearest `global.json` (from the cwd where you run `dotnet test`) with
  `"test": { "runner": "Microsoft.Testing.Platform" }`

Do **not** add `NUnit3TestAdapter`, `ricaun.RevitTest.TestAdapter`, or
`Microsoft.Testing.Extensions.VSTestBridge` to the same project.

## Quick start

Default engine: **NUnit 4.6.1**. TUnit: `TestingFramework=tunit` + pin **TUnit 1.67.0**.

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.1" />
  <PackageReference Include="NUnit" Version="4.6.1" />
  <!-- compile-only host API — pick a package that matches HostVersion -->
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

Full properties, `global.json` placement, conflicts, runner path:
[project-setup.md](references/project-setup.md).

## Run

Run `dotnet test` from a directory covered by the intended `global.json`.

```powershell
dotnet test --project path/to/Host.Tests.csproj -c <Config> --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
```

| Behavior | Detail |
|----------|--------|
| Discovery | `--list-tests`, Test Explorer refresh — **no host** |
| Run | Starts or reuses a host (`ForceLaunch=false` still launches if none match) |
| Passed stdout | `--output Detailed` shows Console / Trace / `Assert.Pass` on passes; default `Normal` expands failures only |
| `--filter` | Method-name regex (NUnit `<name re="1">`), **not** VSTest `FullyQualifiedName~` |
| `[Explicit]` | Selected only when `--filter` matches the method name |
| Host launch | Do not start `Revit.exe` / `acad.exe` yourself |

Filters, `--filter-uid`, exit 8: [mtp-filter.md](references/mtp-filter.md).

## Write tests

Bodies run on the Autodesk API context (not the MTP process). WPF
`Dispatcher.Invoke` is not an API context.

**Paths:** prefer `[CallerFilePath]` for fixtures and agent-visible outputs.
`TestContext.WorkDirectory` is **NUnit-only** and points at a generation shadow —
do not use it for assets or reports. Details: [test-patterns.md](references/test-patterns.md).

| Topic | Reference |
|-------|-----------|
| Shared (paths, smoke, stdout) | [test-patterns.md](references/test-patterns.md) |
| NUnit (default) | [nunit.md](references/nunit.md) |
| TUnit (`TestingFramework=tunit`) | [tunit.md](references/tunit.md) |

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `--filter FullyQualifiedName~…` / `Name=` matches nothing | Use `--filter MethodName` |
| No passed stdout under `dotnet test` | Add `--output Detailed` |
| `[Explicit]` never runs | Select with `--filter MethodName` |
| Exit 8, zero tests | Wrong filter syntax, or cwd `global.json` still has VSTest |
| `"RevitDevTool is not installed"` | Install RevitDevTool or set `TestingRunnerPath` |
| Timeout | Raise `PerTestTimeout` (60s is smoke-only) |
| Stale host state (net48) | Restart the host process |

## Links

- NuGet: [RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter)
- Installer / runner: [RevitDevTool](https://github.com/trgiangv/RevitDevTool)
