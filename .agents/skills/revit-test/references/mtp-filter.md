# MTP filter

| Flag | Owner | What it becomes |
|------|--------|-----------------|
| `--filter` | adapter | NUnit `<name re="1">` regex on `ITest.Name` |
| `--filter-uid` | MTP | Exact TestNode uid → host `<test>` |
| `--treenode-filter` | MTP | Discovered leaves under `/ns/type/method` (never whole assembly) |
| `--list-tests` | MTP | text = DisplayName; `json` = includes `uid` |

Do not pass VSTest expressions (`Name=`, `FullyQualifiedName~`, `Category=`).
They are treated as a literal NUnit name regex and usually match nothing
(**exit 8**).

## Commands

```powershell
dotnet test --project path/to/Host.Tests.csproj -c <Config> --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
dotnet test --project Host.Tests.csproj -c <Config> --list-tests json
dotnet test --project Host.Tests.csproj -c <Config> -- --filter-uid 'Ns.Class.Method("Unit_X")'
```

Use `--` when the SDK binds `--filter` itself. `--filter-uid` needs the uid
from `--list-tests json` (ordinary leaves: `ITest.FullName`;
`TestName`/`SetName`: `Class.Method("DisplayName")`).

`--list-tests` does not start a host. A filtered **run** still executes in the
host. `ForceLaunch=false` starts a matching-version host on run if none is open.

## What matches

| Command | Result |
|---------|--------|
| `--filter Refresh_WritesTheCurrentModel` | That method; unlocks `[Explicit]` |
| `--filter FamilyPolicy` | Every leaf whose `ITest.Name` matches |
| `--filter-uid <uid from json>` | That one TestNode |
| `--filter "FullyQualifiedName~…"` | No match → exit 8 |
| no `--filter` | Whole assembly; `[Explicit]` is **Skipped** |

Exit **8** with zero cases: local select found nothing, or the host filter
matched nothing. Confirm with `--list-tests json` before assuming a dead host.

## Host proof

Test output may include `host-pid=…`. That PID is the Autodesk process, not
the MTP testhost exe.
