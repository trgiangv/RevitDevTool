# Test patterns (shared)

Applies to **NUnit** and **TUnit**. Framework-specific syntax:
[nunit.md](nunit.md) · [tunit.md](tunit.md). Filters: [mtp-filter.md](mtp-filter.md).

## API context

Test bodies run inside the Autodesk host process on the API context thread.
Read `Application` / `Document` from the project's host helper or fixtures — not
from the MTP package.

WPF `Dispatcher.Invoke` is **not** a Revit / AutoCAD API context.

## Paths — default to source

In-host test assemblies are often stream-loaded into the host. Path APIs behave
differently from a normal `dotnet test` project on disk.

| API | Framework | Prefer? |
|-----|-----------|---------|
| `[CallerFilePath]` → folder beside the `.cs` file | Both | **Yes** — fixtures, models, agent-visible outputs |
| `TestContext.CurrentContext.WorkDirectory` | NUnit only | **No** — generation shadow under temp/output |
| `TestContext.CurrentContext.TestDirectory` | NUnit only | **No** — testhost layout, not source tree |
| Framework / engine work directories | TUnit | **No** — same generation-shadow issue |
| `Assembly.GetExecutingAssembly().Location` | Both | **No** — often **empty** when stream-loaded |

Do not write test reports under work directories or `Assembly.Location` — agents
and CI cannot reliably find those paths. Use source-adjacent paths or Console /
`Assert.Pass` for MTP stdout (`--output Detailed`).

```csharp
using System.Runtime.CompilerServices;

static string BesideSource(string relative, [CallerFilePath] string cs = "") =>
    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cs)!, relative));
```

Leave fixtures as `<None>` with no copy-to-output, or equivalent in your SDK.

Framework examples: [nunit.md](nunit.md#paths) · [tunit.md](tunit.md#paths).

## Smoke

Prove the body runs inside the Autodesk process, not the MTP exe. Stdout should
include `host-pid=<Autodesk PID>`.

Replace `RevitAPI` with the host API assembly name for your `HostName` when needed
(e.g. `AcDbMgd` for AutoCAD).

See [nunit.md](nunit.md#smoke) / [tunit.md](tunit.md#smoke) for assert style per engine.

## Output

MTP-mode `dotnet test` needs `--output Detailed` to print passed-test
`Standard output` (Console / Trace / Pass message). Default `Normal` expands
failures only. Do not add extra Trace listeners for the host pane — the adapter
already routes Console / Trace / Debug.
