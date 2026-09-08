using System.Reflection;
using System.Threading.Tasks;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

public sealed class NetfxClosureBindTests
{
    [Fact]
    public void Allows_newer_unifies_stj_nine_onto_ten()
    {
        var requested = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
        Assert.True(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Allows_newer_does_not_downgrade_stj()
    {
        var requested = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Allows_newer_unifies_tasks_extensions_compile_ref_onto_package_identity()
    {
        var requested = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.4.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
        Assert.True(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Allows_newer_does_not_downgrade_tasks_extensions()
    {
        var requested = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.4.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Allows_newer_unifies_any_closure_simple_name()
    {
        var requested = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("Contoso.Component, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
        Assert.True(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Allows_newer_does_not_unify_token_mismatch()
    {
        var requested = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("Contoso.Component, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

        Assert.False(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Try_find_loaded_does_not_scan_the_default_domain()
    {
        var requested = new AssemblyName(
            "Microsoft.Bcl.AsyncInterfaces, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(NetfxClosureBind.TryFindLoaded(requested, [], out _));
    }

    [Fact]
    public void Try_find_loaded_reuses_only_supplied_tasks_extensions()
    {
        var loaded = typeof(ValueTask).Assembly;
        if (!string.Equals(loaded.GetName().Name, "System.Threading.Tasks.Extensions", StringComparison.Ordinal))
            Assert.Skip("ValueTask is not System.Threading.Tasks.Extensions in this testhost.");

        var requested = new AssemblyName(
            "System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.True(NetfxClosureBind.TryFindLoaded(requested, [loaded], out var actual));
        Assert.Same(loaded, actual);
        Assert.False(NetfxClosureBind.TryFindLoaded(requested, [], out _));
    }

    [Fact]
    public void Manifest_unifies_stj_nine_request_onto_ten_when_nine_is_absent()
    {
        using var directory = new TemporaryDirectory();
        var newerPath = Path.Combine(directory.Path, "System.Text.Json.dll");
        File.WriteAllText(newerPath, "placeholder");

        var olderIdentity = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (newerIdentity, new AssemblyCandidate(newerPath, directory.Path)),
        ]);

        Assert.Equal(Path.GetFullPath(newerPath), source.Resolve(olderIdentity)!.Path);
    }

    [Fact]
    public void Manifest_unifies_tasks_extensions_compile_ref_when_older_is_absent()
    {
        using var directory = new TemporaryDirectory();
        var newerPath = Path.Combine(directory.Path, "System.Threading.Tasks.Extensions.dll");
        File.WriteAllText(newerPath, "placeholder");

        var olderIdentity = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.4.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (newerIdentity, new AssemblyCandidate(newerPath, directory.Path)),
        ]);

        Assert.Equal(Path.GetFullPath(newerPath), source.Resolve(olderIdentity)!.Path);
    }

    [Fact]
    public void Manifest_unifies_arbitrary_compile_ref_when_older_is_absent()
    {
        using var directory = new TemporaryDirectory();
        var newerPath = Path.Combine(directory.Path, "Contoso.Component.dll");
        File.WriteAllText(newerPath, "placeholder");

        var olderIdentity = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("Contoso.Component, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (newerIdentity, new AssemblyCandidate(newerPath, directory.Path)),
        ]);

        Assert.Equal(Path.GetFullPath(newerPath), source.Resolve(olderIdentity)!.Path);
    }

    [Fact]
    public void Manifest_does_not_downgrade_stj()
    {
        using var directory = new TemporaryDirectory();
        var olderPath = Path.Combine(directory.Path, "System.Text.Json.dll");
        File.WriteAllText(olderPath, "placeholder");

        var olderIdentity = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (olderIdentity, new AssemblyCandidate(olderPath, directory.Path)),
        ]);

        Assert.Null(source.Resolve(newerIdentity));
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"assembly-isolation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
