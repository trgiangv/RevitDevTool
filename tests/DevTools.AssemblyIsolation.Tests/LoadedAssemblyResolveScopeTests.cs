using DevTools.AssemblyIsolation.Loading;
using System.Reflection;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class LoadedAssemblyResolveScopeTests
{
    [TestMethod]
    public void Open_scope_resolves_a_pathless_assembly_by_full_identity()
    {
        using var directory = new LoadTestDirectory();
        var path = MetadataAssemblySessionTests.CopyFixture("SameSimpleNameV1", "SameSimpleName.dll", directory.Path);
        var pathless = Assembly.Load(File.ReadAllBytes(path));

        using var scope = new LoadedAssemblyResolveScope([pathless]);

        Assert.AreSame(pathless, Assembly.Load(pathless.GetName()));
    }

    [TestMethod]
    public void Scope_does_not_substitute_another_version()
    {
        var loaded = typeof(LoadedAssemblyResolveScopeTests).Assembly;
        using var scope = new LoadedAssemblyResolveScope([loaded]);
        var requested = loaded.GetName();
        requested.Version = new Version(99, 0, 0, 0);

        Assert.IsNull(scope.ResolveForTesting(requested));
        Assert.AreSame(loaded, scope.ResolveForTesting(loaded.GetName()));
    }

    [TestMethod]
    public void Disposed_scope_no_longer_resolves_and_disposal_is_idempotent()
    {
        using var directory = new LoadTestDirectory();
        var path = MetadataAssemblySessionTests.CopyFixture("SameSimpleNameV2", "SameSimpleName.dll", directory.Path);
        var pathless = Assembly.Load(File.ReadAllBytes(path));

        var scope = new LoadedAssemblyResolveScope([pathless]);
        scope.Dispose();
        scope.Dispose();

        Assert.Throws<IOException>(() => Assembly.Load(pathless.GetName()));
    }
}
