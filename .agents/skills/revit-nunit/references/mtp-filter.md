# MTP filter

`--filter` on `dotnet test` is forwarded to the test exe. This package does
**not** parse `Name=` / `FullyQualifiedName~` / `Category=` expressions. The
entire argument becomes NUnit `<name>`.

## Commands

Always `cd` to the **test project folder** (`.csproj` + `global.json`).
Do not run `dotnet test` from the repo root.

```powershell
cd path/to/Host.Tests
dotnet test --project Host.Tests.csproj -c <Config> --filter MethodName
```

If the SDK binds `--filter` itself:

```powershell
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName
```

List tests without starting a host:

```powershell
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
```

Do **not** start `Revit.exe` / `acad.exe`. Runner locates, reuses, or
launches. `ForceLaunch=false` still starts a matching-version host on **run**
if none is open. Discovery (`--list-tests`, Test Explorer refresh) is local
PE metadata.

## What matches

| Command | Result |
|---------|--------|
| `--filter Refresh_WritesTheCurrentModel` | Selects that method; unlocks `[Explicit]` |
| `--filter "Name=Refresh_WritesTheCurrentModel"` | No match → **exit 8, Zero tests ran** |
| `--filter "FullyQualifiedName~Refresh_…"` | Same: literal name, no match |
| no `--filter` | Whole assembly; `[Explicit]` is **Skipped**, not run |

Exit **8** after a few seconds with zero cases = wrong filter, not a dead
host. Retry with the bare method name.

Test Explorer click sends FullName UIDs (`--test` on Runner). CLI must use
the method name.

`[Explicit]` runs only when the filter selects that test. Selecting by
method name is enough.

## Host proof

Test output may include `host-pid=…`. Use that PID to confirm execution
inside the Autodesk process, not the MTP exe.
