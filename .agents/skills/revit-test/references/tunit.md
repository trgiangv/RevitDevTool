# TUnit patterns

Opt in with `TestingFramework=tunit`. Pin **TUnit 1.67.0** (paired with MTP
`Microsoft.Testing.Platform.MSBuild` 2.4.0 from the adapter).

Project setup: [project-setup.md](project-setup.md). Shared rules:
[test-patterns.md](test-patterns.md). Filters: [mtp-filter.md](mtp-filter.md).

```xml
<PropertyGroup>
  <TestingFramework>tunit</TestingFramework>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="TUnit" Version="1.67.0" />
</ItemGroup>
```

**net48:** leave `NetFxModuleInitializer` unset unless another polyfill already
defines `[ModuleInitializer]` — then set it `false`.

## Paths

TUnit has no `TestContext.WorkDirectory`. Use `[CallerFilePath]` — see
[test-patterns.md](test-patterns.md#paths--default-to-source).

```csharp
[Test]
public async Task Reads_fixture_next_to_source()
{
    var path = BesideSource(Path.Combine("Testdata", "model.rvt"));
    await Assert.That(File.Exists(path)).IsTrue();
}
```

## Smoke

```csharp
[Test]
public async Task Runs_inside_host()
{
    var api = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a =>
            string.Equals(a.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));
    await Assert.That(api).IsNotNull();
    Console.WriteLine($"host-pid={Process.GetCurrentProcess().Id}");
}
```

## Explicit / Skip

```csharp
[Explicit("Listed; run only with --filter Explicit_is_listed_not_run")]
public void Explicit_is_listed_not_run() => Assert.Fail("Must not run unless selected.");

[Skip("Listed; must not execute.")]
public void Skipped_is_listed() => Assert.Fail("Skipped tests must not execute.");
```

Select Explicit with `--filter MethodName` (same adapter filter as NUnit).

## Data / hooks (host-relevant)

Common shapes that run in-host: `[Arguments]`, method/class data sources,
property injection, `[Repeat]`, `[Retry]`, `[DependsOn]`, Before/After hooks.
Autodesk API calls in hooks still run on the host API thread via the provider.

Discovery expands data/repeat into leaves locally (no host). Run executes those
leaves inside the host.

## Assert / output

Prefer `await Assert.That(…).…` for TUnit assertions. `Console` / `Trace` /
`Debug` follow the same MTP stdout rules as NUnit (`--output Detailed` for
passed blocks). `Assert.Fail("…")` surfaces on the failure node.

## Timeout

`PerTestTimeout` is the adapter per-test budget. TUnit `[Timeout]` is
framework-local; do not assume it cancels Revit API mid-call on the main thread.
