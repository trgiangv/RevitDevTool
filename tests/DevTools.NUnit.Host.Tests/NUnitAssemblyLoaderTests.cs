using DevTools.NUnit.Host;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitAssemblyLoaderTests
{
    [Fact]
    public void Preflight_fails_when_assembly_missing()
    {
        var result = NUnitAssemblyLoader.Preflight(@"C:\missing\assembly.dll");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Preflight_succeeds_for_existing_assembly()
    {
        var path = typeof(NUnitAssemblyLoaderTests).Assembly.Location;

        var result = NUnitAssemblyLoader.Preflight(path);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(path), result.AssemblyPath);
    }

    [Fact]
    public void ResolveAssemblyPath_returns_full_path_for_existing_assembly()
    {
        var path = typeof(NUnitAssemblyLoaderTests).Assembly.Location;

        var resolved = NUnitAssemblyLoader.ResolveAssemblyPath(path);

        Assert.Equal(Path.GetFullPath(path), resolved);
    }

    [Fact]
    public void EnsureLoadable_throws_when_preflight_fails()
    {
        var ex = Assert.Throws<NUnitAssemblyLoadException>(
            () => NUnitAssemblyLoader.EnsureLoadable(@"C:\missing\assembly.dll"));

        Assert.False(ex.Result.Success);
    }
}
