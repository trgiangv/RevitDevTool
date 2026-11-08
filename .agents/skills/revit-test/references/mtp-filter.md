# MTP filter

| Flag | Owner | What it becomes |
|------|-------|-----------------|
| `--filter` | adapter | Method name or substring. NUnit sends it as `<name re="1">`. TUnit and MSTest match method name, display name, full name, or a case-insensitive substring |
| `--filter-uid` | MTP | Exact TestNode uid, sent to the host as `<test>` |
| `--treenode-filter` | MTP | Discovered leaves under `/ns/type/method` (never the whole assembly) |
| `--list-tests` | MTP | Text prints DisplayName; `json` includes `uid` |

VSTest expressions (`Name=`, `FullyQualifiedName~`, `Category=`) are read as a
literal NUnit name regex and usually match nothing, which ends in exit 8.

## Commands

```powershell
dotnet test --project path/to/Host.Tests.csproj -c <Config> --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> -- --filter MethodName --output Detailed
dotnet test --project Host.Tests.csproj -c <Config> --list-tests
dotnet test --project Host.Tests.csproj -c <Config> --list-tests json
dotnet test --project Host.Tests.csproj -c <Config> -- --filter-uid 'Ns.Class.Method("Unit_X")'
```

Put `--filter` after `--` when the SDK binds it itself. Take the `--filter-uid`
value from `--list-tests json` (ordinary leaves: `ITest.FullName`;
`TestName`/`SetName`: `Class.Method("DisplayName")`).

`--list-tests` starts no host. A filtered run still executes in the host, and
`ForceLaunch=false` starts a matching-version host if none is open.

## What matches

| Command | Result |
|---------|--------|
| `--filter Refresh_WritesTheCurrentModel` | That method; also selects `[Explicit]` |
| `--filter FamilyPolicy` | Every leaf whose `ITest.Name` matches |
| `--filter-uid <uid from json>` | That one TestNode |
| `--filter "FullyQualifiedName~…"` | No match, exit 8 |
| no `--filter` | Whole assembly; `[Explicit]` tests are Skipped |

Exit 8 with zero cases means the local selection found nothing or the host
filter matched nothing. Check with `--list-tests json` before suspecting the host.

## Host proof

Test output may include `host-pid=…`. That PID is the Autodesk process, not the
MTP testhost exe.
