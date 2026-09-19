# NUnit patterns

Default engine (`TestingFramework` unset or `nunit`). Pin **NUnit 4.6.1** — the
host rejects a missing or mismatched `nunit.framework`.

Shared rules: [test-patterns.md](test-patterns.md). Filters: [mtp-filter.md](mtp-filter.md).

## Paths

NUnit exposes generation shadows via `TestContext`. Prefer `[CallerFilePath]` —
see [test-patterns.md](test-patterns.md#paths--default-to-source).

```csharp
[Test]
public void Reads_fixture_next_to_source()
{
    var path = BesideSource(Path.Combine("Testdata", "model.rvt"));
    Assert.That(File.Exists(path), Is.True, path);
}

[Test]
public void Writes_report_next_to_source()
{
    var outPath = BesideSource(Path.Combine("Testdata", "last-run.txt"));
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, $"host-pid={Process.GetCurrentProcess().Id}");
}
```

(`BesideSource` helper defined in [test-patterns.md](test-patterns.md).)

## Smoke

```csharp
[Test]
public void Runs_inside_host()
{
    var api = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a =>
            string.Equals(a.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));
    Assert.That(api, Is.Not.Null);
    Console.WriteLine($"host-pid={Process.GetCurrentProcess().Id}");
}
```

## Explicit

```csharp
[Explicit("Destructive; run with --filter Refresh_WritesTheCurrentModel")]
[Test]
public void Refresh_WritesTheCurrentModel() { }
```

Without `--filter`, Explicit tests are **Skipped**. VSTest `Name=` /
`FullyQualifiedName~` do not unlock them — use `--filter MethodName`.

## Lifecycle

`[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, `[SetUpFixture]` run on each host run.
Static fields on **net48** survive across `dotnet test` on the same host PID —
restart the host if state is dirty.

## Assert / output

| Call | CLI with `--output Detailed` |
|------|------------------------------|
| `Console` / `TestContext.WriteLine` | `Standard output` |
| `Trace` / `Debug` | Merged into `Standard output` |
| `Assert.Pass("…")` | Merged into `Standard output` |
| `Assert.Fail("…")` | Failure message / stack |

## Timeout

Raise `PerTestTimeout` for slow cases. Main-thread API work cannot cancel mid-call.
