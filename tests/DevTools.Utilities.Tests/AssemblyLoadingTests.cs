using System.Reflection;
using System.Runtime.Loader;
using DevTools.Utilities.AssemblyLoading;

namespace DevTools.Utilities.Tests;

public sealed class AssemblyLoadingTests
{
    [Fact]
    public void ResolveFromAppDomain_finds_already_loaded_assembly()
    {
        var assembly = typeof(AssemblyLoadingTests).Assembly;
        var resolved = HostAssemblyResolver.ResolveFromAppDomain(assembly.GetName());

        Assert.Same(assembly, resolved);
    }

    [Fact]
    public void HostSharedAssemblies_treats_system_prefix_as_shared()
    {
        Assert.True(HostSharedAssemblies.IsShared("System.Text.Json"));
        Assert.False(HostSharedAssemblies.IsShared("MyCustomPlugin"));
    }

    [Fact]
    public void HostSharedAssemblies_package_prefix_excludes_microsoft_extensions()
    {
        Assert.True(HostSharedAssemblies.IsShared("Microsoft.Extensions.Logging.Abstractions"));
        Assert.False(HostSharedAssemblies.MatchesHostPackagePrefix("Microsoft.Extensions.Logging.Abstractions"));
        Assert.True(HostSharedAssemblies.MatchesHostPackagePrefix("Autodesk.Revit.DB"));
        Assert.True(HostSharedAssemblies.MatchesHostPackagePrefix("MahApps.Metro"));
        Assert.True(HostSharedAssemblies.MatchesHostPackagePrefix("ControlzEx.Theming"));
        Assert.True(HostSharedAssemblies.MatchesHostPackagePrefix("CommunityToolkit.Mvvm"));
    }

    [Fact]
    public void DirectoryAssemblyLoad_returns_already_loaded_assembly_from_same_directory()
    {
        var assembly = typeof(AssemblyLoadingTests).Assembly;
        var directory = Path.GetDirectoryName(assembly.Location)
            ?? throw new InvalidOperationException("Test assembly location is unavailable.");

        var resolved = DirectoryAssemblyLoad.TryLoad(directory, assembly.GetName());

        Assert.NotNull(resolved);
        Assert.Equal(assembly.GetName().Name, resolved!.GetName().Name);
        Assert.Same(resolved, DirectoryAssemblyLoad.TryLoad(directory, assembly.GetName()));
    }

    [Fact]
    public void DirectoryAssemblyLoad_skips_shared_assemblies_not_in_appdomain()
    {
        var directory = Path.GetTempPath();
        var resolved = DirectoryAssemblyLoad.TryLoad(directory, new AssemblyName("System.Text.Json"));

        Assert.Null(resolved);
    }

    [Fact]
    public void DirectoryAssemblyLoad_shadow_load_does_not_lock_source_and_reloads_on_change()
    {
        var source = typeof(AssemblyLoadingTests).Assembly.Location;
        var tempDir = Path.Combine(Path.GetTempPath(), "devtools-dir-load-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var probePath = Path.Combine(tempDir, Path.GetFileName(source));
            File.Copy(source, probePath);

            var first = DirectoryAssemblyLoad.LoadPath(probePath);
            Assert.Equal(typeof(AssemblyLoadingTests).Assembly.GetName().Name, first.GetName().Name);
            Assert.Same(first, DirectoryAssemblyLoad.LoadPath(probePath));

            // Source must remain writable (shadow LoadFile locks only the temp copy).
            using (var stream = new FileStream(probePath, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                stream.SetLength(stream.Length); // touch without corrupting PE for same stamp path
            }

            // Force a new stamp and a different shadow identity.
            File.SetLastWriteTimeUtc(probePath, DateTime.UtcNow.AddMinutes(1));
            var second = DirectoryAssemblyLoad.LoadPath(probePath);
            Assert.NotSame(first, second);
            Assert.Equal(first.GetName().Name, second.GetName().Name);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void ByteAssemblyLoader_LoadFromStream_does_not_lock_source_file()
    {
        var source = typeof(AssemblyLoadingTests).Assembly.Location;
        var tempDir = Path.Combine(Path.GetTempPath(), "devtools-byte-load-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var alc = new AssemblyLoadContext("byte-load-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            var probePath = Path.Combine(tempDir, Path.GetFileName(source));
            File.Copy(source, probePath);

            var loaded = ByteAssemblyLoader.LoadFromStream(alc, probePath);
            Assert.Equal(typeof(AssemblyLoadingTests).Assembly.GetName().Name, loaded.GetName().Name);

            using var stream = new FileStream(probePath, FileMode.Open, FileAccess.Write, FileShare.Read);
            stream.SetLength(stream.Length);
        }
        finally
        {
            alc.Unload();
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
