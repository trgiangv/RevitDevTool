---
name: revit-nunit
description: >
  Configure and run in-host tests with the RevitDevTool.TestAdapter NuGet
  package (Microsoft Testing Platform). Use in any repo that references that
  package when writing or running NUnit tests inside Revit, AutoCAD, or
  Civil 3D; setting HostName/HostVersion/ForceLaunch; using `dotnet test --filter`;
  selecting [Explicit] tests; or diagnosing MTP exit code 8 / zero tests.
---

# Host NUnit tests (RevitDevTool.TestAdapter)

Standalone consumer skill. Copy this folder into any repo (or
`~/.agents/skills/revit-nunit/`).

```
dotnet test (MTP exe) → installed DevTools.TestRunner → host pipe → NUnit 4.6.1
```

The MTP exe never runs test bodies locally. Requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool) installed and NuGet
`RevitDevTool.TestAdapter`.

Detect: `PackageReference` `RevitDevTool.TestAdapter` + `global.json` in **that
project folder** (not the repo root) with
`"test": { "runner": "Microsoft.Testing.Platform" }`.

## Configure

Pin **NUnit 4.6.1**. Do not add `NUnit3TestAdapter` or
`ricaun.RevitTest.TestAdapter`.

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
  <OutputType>Exe</OutputType>
  <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
  <HostName>Revit</HostName>
  <HostVersion>2024</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" />
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="NUnit" Version="4.6.1" />
</ItemGroup>
```

Create `global.json` **in the test project folder** (same directory as the
`.csproj`). Do **not** put it at the repo root — that forces every test
project in the tree onto MTP.

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

Property meanings, RID/`OutputType`, and conflicting packages:
[project-setup.md](references/project-setup.md).

## Run

Always `cd` to the **test project folder** (where `global.json` and the
`.csproj` live) before `dotnet test`. Do not run from the repo root.

```powershell
cd path/to/Host.Tests
dotnet test --project Host.Tests.csproj -c <Config> --filter MethodName
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
```

`--filter` is the **NUnit method name only**. Same command runs `[Explicit]`.
Do not start `Revit.exe` / `acad.exe` yourself.

Filter / exit 8: [mtp-filter.md](references/mtp-filter.md).

## Write tests

Bodies run on the Autodesk API context. Use the host context type for
`Application`, `TestContext.WorkDirectory` for assets. Patterns:
[test-patterns.md](references/test-patterns.md).

## Common mistakes

| Mistake | Fix |
|---------|-----|
| `--filter "Name=…"` / `FullyQualifiedName~` | `--filter MethodName` |
| `[Explicit]` never runs | Select it with `--filter MethodName` |
| No exe / test project does not run | Set `OutputType=Exe` and `RuntimeIdentifiers=win-x64` in the csproj |
| Ran from repo root / another project | `cd` to the test project folder that has `global.json` |
| Timeout | Raise `PerTestTimeout` (per-test budget; 60s is smoke-only) |

## Package

- NuGet: [RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter)
- Installer / Runner: [RevitDevTool](https://github.com/trgiangv/RevitDevTool)

## References

- [project-setup.md](references/project-setup.md)
- [mtp-filter.md](references/mtp-filter.md)
- [test-patterns.md](references/test-patterns.md)
