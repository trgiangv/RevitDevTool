# Test patterns (shared)

Applies to NUnit and TUnit. Framework syntax: [nunit.md](nunit.md),
[tunit.md](tunit.md), [mstest.md](mstest.md). Filters: [mtp-filter.md](mtp-filter.md).

## API context

Test bodies run inside the Autodesk host process on the API context thread.
Read `Application` / `Document` from the project's host helper or fixtures, not
from the MTP package. WPF `Dispatcher.Invoke` is not a Revit or AutoCAD API context.

## Paths — default to source

In-host test assemblies are often stream-loaded, so path APIs behave
differently from a normal `dotnet test` project on disk.

| API | Framework | Use for assets and outputs |
|-----|-----------|----------------------------|
| `[CallerFilePath]`, folder beside the `.cs` file | Both | Yes: fixtures, models, agent-visible outputs |
| `TestContext.CurrentContext.WorkDirectory` | NUnit only | No: generation shadow under temp or output |
| `TestContext.CurrentContext.TestDirectory` | NUnit only | No: testhost layout, not the source tree |
| Framework or engine work directories | TUnit | No: same generation-shadow issue |
| `Assembly.GetExecutingAssembly().Location` | Both | No: often empty when stream-loaded |

Write reports beside the source, or to Console / `Assert.Pass` for MTP stdout
(`--output Detailed`), so agents and CI can find them.

```csharp
using System.Runtime.CompilerServices;

static string BesideSource(string relative, [CallerFilePath] string cs = "") =>
    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cs)!, relative));
```

Leave fixtures as `<None>` with no copy-to-output, or the equivalent in your SDK.

Framework examples: [nunit.md](nunit.md#paths), [tunit.md](tunit.md#paths).

## Smoke

Prove the body runs inside the Autodesk process, not the MTP exe: stdout should
include `host-pid=<Autodesk PID>`. Replace `RevitAPI` with the host API assembly
for your `HostName` when needed (e.g. `AcDbMgd` for AutoCAD). Assert style per
engine: [nunit.md](nunit.md#smoke), [tunit.md](tunit.md#smoke).

## Output

MTP-mode `dotnet test` needs `--output Detailed` to print passed-test
`Standard output` (Console, Trace, Pass message); the default `Normal` expands
failures only. The adapter already routes Console, Trace, and Debug, so no extra
Trace listener is needed for the host pane.
