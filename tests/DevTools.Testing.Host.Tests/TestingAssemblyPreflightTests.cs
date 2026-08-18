using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

public sealed class TestingAssemblyPreflightTests
{
    [Fact]
    public void Preflight_reports_a_missing_assembly_without_framework_types()
    {
        var result = TestingAssemblyPreflight.Check(@"C:\missing\assembly.dll");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveAndEnsureLoadable_returns_the_normalized_managed_assembly_path()
    {
        var path = typeof(TestingAssemblyPreflightTests).Assembly.Location;

        var resolved = TestingAssemblyPreflight.ResolveAndEnsureLoadable(path);

        Assert.Equal(Path.GetFullPath(path), resolved);
    }
}
