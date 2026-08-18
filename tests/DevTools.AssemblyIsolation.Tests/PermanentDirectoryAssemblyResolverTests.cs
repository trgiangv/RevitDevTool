using System.Reflection;
using DevTools.AssemblyIsolation.Loading;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class PermanentDirectoryAssemblyResolverTests
{
    [Fact]
    public void Registered_resolver_probes_its_directory_for_managed_dependencies()
    {
        using var directory = new PermanentLoadDirectory();
        var entryPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "IsolationEntry.dll", directory.Path);
        MetadataAssemblySessionTests.CopyFixture("PrivateSystemNamedDependency", "System.Private.IsolationFixture.dll", directory.Path);
        var loader = new PermanentAssemblyLoader();
        using var resolver = PermanentDirectoryAssemblyResolver.Create(directory.Path, loader);
        resolver.Register();
        resolver.Register();
        var entry = loader.LoadPath(entryPath);

        var result = (string)entry.GetType("IsolationEntry.Entry")!
            .GetMethod("GetPrivateDependencyName")!
            .Invoke(null, null)!;

        Assert.Equal("System.Private.IsolationFixture", new AssemblyName(result).Name);
    }

    [Fact]
    public void Disposed_resolver_no_longer_probes_its_directory_and_disposal_is_idempotent()
    {
        using var directory = new PermanentLoadDirectory();
        var entryPath = MetadataAssemblySessionTests.CopyFixture("IsolationEntry", "IsolationEntry.dll", directory.Path);
        MetadataAssemblySessionTests.CopyFixture("PrivateAfterDisposeDependency", "System.Private.AfterDisposeFixture.dll", directory.Path);
        var loader = new PermanentAssemblyLoader();
        var resolver = PermanentDirectoryAssemblyResolver.Create(directory.Path, loader);
        resolver.Register();
        var entry = loader.LoadPath(entryPath);
        resolver.Dispose();
        resolver.Dispose();

        var exception = Assert.Throws<TargetInvocationException>(() => entry.GetType("IsolationEntry.Entry")!
            .GetMethod("GetAfterDisposeDependencyName")!
            .Invoke(null, null));

        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void Registered_resolver_probes_its_directory_for_unmanaged_dependencies()
    {
        using var directory = new PermanentLoadDirectory();
        var source = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        var destination = Path.Combine(directory.Path, "resolver-native-fixture.dll");
        File.Copy(source, destination);
        using var resolver = PermanentDirectoryAssemblyResolver.Create(directory.Path, new PermanentAssemblyLoader());

        var resolvedPath = resolver.FindUnmanagedPathForTesting("resolver-native-fixture");

        Assert.Equal(destination, resolvedPath, ignoreCase: true);
    }
}
