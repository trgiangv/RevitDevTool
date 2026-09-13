---
name: revit-test
description: >
  Configure and run in-host tests with the RevitDevTool.TestAdapter NuGet
  package (Microsoft Testing Platform). Use in any repo that references that
  package when writing or running NUnit or TUnit tests inside Revit, AutoCAD,
  or Civil 3D; setting HostName/HostVersion/ForceLaunch; using `dotnet test --filter`;
  selecting [Explicit] tests; or diagnosing MTP exit code 8 / zero tests.
---

# Host tests (RevitDevTool.TestAdapter)

Standalone consumer skill. Copy this folder into any repo (or
`~/.agents/skills/revit-test/`).

```
dotnet test (MTP exe) → installed DevTools.TestRunner → host pipe → NUnit or TUnit
```

The MTP exe never runs test bodies locally. Requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool) installed and NuGet
`RevitDevTool.TestAdapter`.

Detect: `PackageReference` `RevitDevTool.TestAdapter` + a `global.json` with
`"test": { "runner": "Microsoft.Testing.Platform" }` — **repo root** when the
tree is MTP (or has one VSTest folder that overrides to `"runner": "VSTest"`),
otherwise next to the MTP test project.

## Configure

Default engine is NUnit (`4.6.1`). Do not add `NUnit3TestAdapter` or
`ricaun.RevitTest.TestAdapter`. TUnit: set `TestingFramework` to `tunit` and
pin `TUnit` `1.67.0`.

```xml
<PropertyGroup>
  <HostName>Revit</HostName>
  <HostVersion>2025</HostVersion>
  <ForceLaunch>false</ForceLaunch>
  <PerTestTimeout>60</PerTestTimeout>
  <LaunchTimeout>360</LaunchTimeout>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.1.0"/>
  <PackageReference Include="NUnit" Version="4.6.1" />
  <PackageReference Include="Revit_All_Main_Versions_API_x64" Version="2025.0.*"
    IncludeAssets="build; compile" PrivateAssets="All" />
</ItemGroup>
```

`dotnet test` needs `"test": { "runner": "Microsoft.Testing.Platform" }` in
`global.json`. All-MTP repo, or one VSTest leftover with a nested `"runner":
"VSTest"`: put MTP on the **root**. Many VSTest/`dotnet test` projects still in
the tree: scope MTP next to the MTP test project.

```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestMinor" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

If the repo has a central `GlobalPackageReference` to `Polyfill`, remove it on
the test project (`<GlobalPackageReference Remove="Polyfill" />`). NUnit does
not need it; net48 TUnit gets `[ModuleInitializer]` from the package.
`NetFxModuleInitializer=false` opts out when that skip misses your type
(see [project-setup.md](references/project-setup.md)).

Property meanings and conflicting packages:
[project-setup.md](references/project-setup.md).

## Run

Run `dotnet test` from a directory covered by the intended `global.json`
(repo root for MTP). `cd` into a `"runner": "VSTest"` folder before testing
that project.

```powershell
dotnet test --project path/to/Host.Tests/Host.Tests.csproj -c <Config> --filter MethodName
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
```

`--filter` is an adapter method-name option (NUnit `<name re="1">` regex, so
`FamilyPolicy` matches those cases). Same command runs `[Explicit]`. Do
not start `Revit.exe` / `acad.exe` yourself. `--filter-uid` needs the UID
from `--list-tests json` (ordinary leaves: `ITest.FullName`; `TestName` /
`SetName`: `Class.Method("DisplayName")`). PowerShell: quote uids that
contain `"` (`--filter-uid 'Ns.Class.Method("Unit_X")'`).

Filter / exit 8: [mtp-filter.md](references/mtp-filter.md).

## Write tests

Bodies run on the Autodesk API context. Use the host context type for
`Application`, `TestContext.WorkDirectory` for assets. Patterns:
[test-patterns.md](references/test-patterns.md).

## Common mistakes

| Mistake | Fix |
|---------|-----|
| `--filter "Name=…"` / `FullyQualifiedName~` | `--filter MethodName` or a substring |
| `[Explicit]` never runs | Select it with `--filter MethodName` |
| Ran `dotnet test` on a VSTest project from an MTP `global.json` cwd | `cd` into the folder whose `global.json` has `"runner": "VSTest"` |
| Timeout | Raise `PerTestTimeout` (per-test budget; 60s is smoke-only) |

## Package

- NuGet: [RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter)
- Installer / Runner: [RevitDevTool](https://github.com/trgiangv/RevitDevTool)

## References

- [project-setup.md](references/project-setup.md)
- [mtp-filter.md](references/mtp-filter.md)
- [test-patterns.md](references/test-patterns.md)
