# MSTest patterns

Opt in with `TestingFramework=mstest`. The project is `Sdk="MSTest.Sdk/4.5.1"`
(or `Microsoft.NET.Sdk` with `EnableMSTestRunner=true`). Pin **4.5.1**. The
in-host closure rejects a different MSTest.TestFramework or MTP version.

Project setup: [project-setup.md](project-setup.md). Shared rules:
[test-patterns.md](test-patterns.md). Filters: [mtp-filter.md](mtp-filter.md).

```xml
<Project Sdk="MSTest.Sdk/4.5.1">
  <PropertyGroup>
    <TestingFramework>mstest</TestingFramework>
  </PropertyGroup>
</Project>
```

`MSTest.Sdk` brings the framework. Do not add `NUnit3TestAdapter` or a
`.runsettings` file. `NetFxModuleInitializer` does not apply.

Selecting one test also runs its `[DependsOn]` prerequisites (method and
class, transitive). Both the testhost discoverer and the in-host session add
them before `--filter-uid`.

## Paths

MSTest has no generation work directory for assets. Use `[CallerFilePath]` —
see [test-patterns.md](test-patterns.md#paths--default-to-source).

## Smoke

```csharp
[TestClass]
public sealed class HostSmokeTests
{
    [TestMethod]
    public void Runs_inside_host()
    {
        var api = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a =>
                string.Equals(a.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(api);
    }
}
```

`Console.WriteLine`, `Trace`, and `Debug` land on the test's standard output,
including when the test passes.
