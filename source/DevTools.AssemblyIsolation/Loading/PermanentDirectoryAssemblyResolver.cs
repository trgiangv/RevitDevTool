using System.Reflection;
using DevTools.AssemblyIsolation.Sources;
#if NET
using System.Runtime.InteropServices;
using System.Runtime.Loader;
#endif

namespace DevTools.AssemblyIsolation.Loading;

public sealed class PermanentDirectoryAssemblyResolver : IDisposable
{
    readonly DirectoryAssemblySource managedSource;
    readonly PermanentAssemblyLoader loader;
    readonly string directory;
    bool registered;
    bool disposed;

    PermanentDirectoryAssemblyResolver(string directory, PermanentAssemblyLoader loader)
    {
        this.directory = Path.GetFullPath(directory);
        managedSource = new DirectoryAssemblySource(this.directory, "permanent add-in directory");
        this.loader = loader;
    }

    public static PermanentDirectoryAssemblyResolver Create(string directory, PermanentAssemblyLoader loader)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A directory is required.", nameof(directory));
        if (loader is null)
            throw new ArgumentNullException(nameof(loader));

        return new PermanentDirectoryAssemblyResolver(directory, loader);
    }

    public void Register()
    {
        ThrowIfDisposed();
        if (registered)
            return;

#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += ResolveManaged;
#else
        AssemblyLoadContext.Default.Resolving += ResolveManaged;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveUnmanaged;
        loader.LoadContext.Resolving += ResolveManaged;
        loader.LoadContext.ResolvingUnmanagedDll += ResolveUnmanaged;
#endif
        registered = true;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        if (registered)
        {
#if NETFRAMEWORK
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveManaged;
#else
            AssemblyLoadContext.Default.Resolving -= ResolveManaged;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll -= ResolveUnmanaged;
            loader.LoadContext.Resolving -= ResolveManaged;
            loader.LoadContext.ResolvingUnmanagedDll -= ResolveUnmanaged;
#endif
            registered = false;
        }

        disposed = true;
    }

#if NETFRAMEWORK
    Assembly? ResolveManaged(object? sender, ResolveEventArgs args) => ResolveManaged(new AssemblyName(args.Name));
#else
    Assembly? ResolveManaged(AssemblyLoadContext context, AssemblyName requested) => ResolveManaged(requested);
#endif

    Assembly? ResolveManaged(AssemblyName requested)
    {
        if (disposed)
            return null;

        var candidate = managedSource.Resolve(requested);
        return candidate is null ? null : loader.LoadPath(candidate.Path);
    }

#if NET
    IntPtr ResolveUnmanaged(Assembly assembly, string unmanagedDllName) => ResolveUnmanaged(unmanagedDllName);

    internal string? FindUnmanagedPathForTesting(string unmanagedDllName) => FindUnmanagedPath(unmanagedDllName);

    IntPtr ResolveUnmanaged(string unmanagedDllName)
    {
        var candidate = FindUnmanagedPath(unmanagedDllName);
        return candidate is null ? IntPtr.Zero : NativeLibrary.Load(candidate);
    }

    string? FindUnmanagedPath(string unmanagedDllName)
    {
        if (disposed || string.IsNullOrWhiteSpace(unmanagedDllName))
            return null;
        var fileName = Path.GetFileName(unmanagedDllName);
        if (!string.Equals(fileName, unmanagedDllName, StringComparison.Ordinal))
            return null;
        if (!Path.HasExtension(fileName))
            fileName += ".dll";

        var candidate = Path.Combine(directory, fileName);
        return File.Exists(candidate) ? candidate : null;
    }
#endif

    void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PermanentDirectoryAssemblyResolver));
    }
}
