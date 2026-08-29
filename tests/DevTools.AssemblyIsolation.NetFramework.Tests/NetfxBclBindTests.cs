using System.Reflection;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

public sealed class NetfxBclBindTests
{
    [Fact]
    public void Allows_newer_unifies_stj_nine_onto_ten()
    {
        var requested = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
        Assert.True(NetfxBclBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Allows_newer_does_not_downgrade_stj()
    {
        var requested = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.False(NetfxBclBind.AllowsNewer(requested, candidate));
    }

    [Fact]
    public void Allows_newer_does_not_unify_non_bcl_simple_names()
    {
        var requested = new AssemblyName("Contoso.Component, Version=1.0.0.0");
        var candidate = new AssemblyName("Contoso.Component, Version=2.0.0.0");

        Assert.False(NetfxBclBind.AllowsNewer(requested, candidate));
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
