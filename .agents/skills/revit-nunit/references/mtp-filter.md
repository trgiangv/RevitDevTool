# MTP filter

Measured on the generated test exe (`--info` / `--help`), not inferred
from MTP version notes.

| Flag | Owner | Arity | What it becomes |
|------|--------|-------|-----------------|
| `--filter` | adapter `HostCommandLineProvider` | 1 | `TestingSelection.Names` → NUnit `<name>` (regex on `ITest.Name`) |
| `--filter-uid` | Microsoft.Testing.Platform | 1..N | `TestNodeUidListFilter` → `TestingSelection.TestIds` → NUnit `<test>` (`ITest.FullName`) |
| `--list-tests` text | platform | — | prints `TestNode.DisplayName` (`ITest.Name`), not the UID |
| `--list-tests json` | platform | — | `uid` is `TestNode.Uid` = NUnit `ITest.FullName` |

`dotnet test --filter MethodName` from the test-project folder reaches the
adapter option. Do not pass `Name=` / `FullyQualifiedName~` / `Category=` —
those strings are sent as a NUnit name regex and will not match.

`--filter-uid` must be the json uid. Ordinary cases are `ITest.FullName`.
`TestName`/`SetName` leaves are `Class.Method("DisplayName")`.

## Commands

Always `cd` to the **test project folder** (`.csproj` + `global.json`).
Do not run `dotnet test` from the repo root.

```powershell
cd path/to/Host.Tests
dotnet test --project Host.Tests.csproj -c <Config> --filter MethodName
dotnet test --project Host.Tests.csproj -c <Config> --filter FamilyPolicy
```

If the SDK binds `--filter` itself:

```powershell
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName
```

```powershell
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
dotnet test --project Host.Tests.csproj -c <Config> --list-tests json
dotnet test --project Host.Tests.csproj -c <Config> -- --filter-uid <uid>
```

List tests without starting a host. When NUnit can `GetTypes()` the
assembly (plain NUnit fixtures), discovery is `ExploreTests` and UIDs
are `ITest.FullName`. Revit/AutoCAD test exes typically cannot load in
the MTP process (`ReflectionTypeLoadException`); list-tests then falls
back to PE metadata. `--filter MethodName` still goes to the host as
NUnit `<name>` regex — it is not rewritten into PE `Type.Method` ids.

`ForceLaunch=false` still starts a matching-version host on **run** if none
is open.

## What matches

| Command | Result |
|---------|--------|
| `--filter Refresh_WritesTheCurrentModel` | NUnit name regex; that method; unlocks `[Explicit]` |
| `--filter FamilyPolicy` | Every leaf whose `ITest.Name` matches the regex, including parameterized cases |
| `--filter-uid <FullName from json>` | That one NUnit test |
| `--filter "Name=Refresh_WritesTheCurrentModel"` | No match → **exit 8, Zero tests ran** |
| `--filter "FullyQualifiedName~Refresh_…"` | Same: literal token, no match |
| no `--filter` | Whole assembly; `[Explicit]` is **Skipped**, not run |

Exit **8** after a few seconds with zero cases: local NUnit select found
nothing (fast) **or** the host ran a filter that matched zero loaded tests.
Confirm with `--list-tests json` before assuming a dead host.

Test Explorer click / `--filter-uid` send discovered UIDs (`ITest.FullName`).
A testhost stub or identifier `Class.Method` also runs in-host expansions
of that method, including `TestName` / `SetName` leaves.

`[Explicit]` runs only when the filter selects that test.

## Host proof

Test output may include `host-pid=…`. Use that PID to confirm execution
inside the Autodesk process, not the MTP exe.
