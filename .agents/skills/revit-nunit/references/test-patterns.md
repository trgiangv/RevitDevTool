# Test patterns

Bodies run on the Autodesk API context (`RunOnMainThread`). WPF
`Dispatcher.Invoke` is not an API context. Read `Application` from the
host's context type, not from the MTP package.

`Assembly.Location` is empty (stream-load). Locate Content/assets with
`TestContext.WorkDirectory` (generation shadow of the test output).

## 1. Smoke — prove the host process

```csharp
[Test]
public void Runs_inside_revit()
{
    var revitApi = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a =>
            string.Equals(a.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));

    Assert.That(revitApi, Is.Not.Null, "Host tests must execute inside Revit, not the MTP exe.");
    Console.WriteLine($"host-pid={Process.GetCurrentProcess().Id}");
}
```

## 2. Host application context

Use the project's host helper (Inspexel `RevitContext`, Nice3point
`RevitApiContext`, …):

```csharp
[Test]
public void Reads_application_version()
{
    var version = RevitApiContext.Application.VersionBuild;
    Assert.That(version, Is.Not.Null.And.Not.Empty);
    Console.WriteLine(version);
}
```

## 3. Content files via WorkDirectory

```csharp
[Test]
public void Loads_content_from_shadow()
{
    var path = Path.Combine(TestContext.WorkDirectory, "Testdata", "model.rvt");
    Assert.That(File.Exists(path), Is.True, path);
}
```

Mark files `<Content CopyToOutputDirectory="PreserveNewest" />` so they copy
into the generation shadow.

## 4. One-shot / Explicit

```csharp
[Explicit("Writes the live model; run with --filter Refresh_WritesTheCurrentModel")]
[Test]
public void Refresh_WritesTheCurrentModel()
{
    // selected only when --filter names this method
}
```

Without `--filter`, NUnit skips Explicit. Do not expect `Name=` expressions
to unlock it — see [mtp-filter.md](mtp-filter.md).

## 5. Setup lifecycle

`[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, `[SetUpFixture]` are NUnit's.
They run again on each `nunit/run`. User **static fields** on net48 do
**not** reset between `dotnet test` invocations on the same host PID.
Restart the host if static or event state is dirty.

```csharp
[SetUpFixture]
public sealed class AssemblySetUp
{
    [OneTimeSetUp]
    public void Init() { /* per run, not per process */ }
}

[TestFixture]
public sealed class Fixture
{
    [OneTimeSetUp]
    public void FixtureInit() { }

    [SetUp]
    public void PerTest() { }
}
```

net8+ hosts isolate a **rebuilt** generation by content hash. Live unload
is not guaranteed. Same-generation re-runs still share statics.

## 6. Output

| API | IDE / `dotnet test` stdout | Host log pane (tracing on) |
|-----|----------------------------|----------------------------|
| `Console.WriteLine` / `TestContext.WriteLine` | Yes (`CaseResult.Output`) | Forwarded at case finish |
| `Trace.WriteLine` / `Debug.WriteLine` | Merged into stdout | Process `Trace.Listeners` |

Do not add extra listeners to "help" the pane.

## 7. Timeout

`HostTimeout` covers the entire `nunit/run`, not one assertion. Raise it
for large suites. NUnit `RunOnMainThread` cannot cancel an in-flight test.
