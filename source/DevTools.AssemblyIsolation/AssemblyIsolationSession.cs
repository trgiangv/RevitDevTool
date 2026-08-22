using System.Reflection;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Runtime;
#if NET
using System.Runtime.CompilerServices;
using DevTools.AssemblyIsolation.Loading;
#endif

namespace DevTools.AssemblyIsolation;

public sealed class AssemblyIsolationSession : IDisposable
{
#if NET
    private readonly bool isCollectible;
    private CollectibleAssemblyIsolationContext? collectibleContext;
    private readonly WeakReference? collectibleContextReference;
#endif
    private NetFrameworkAssemblyIsolationScope? netFrameworkScope;
    private bool disposed;

    private AssemblyIsolationSession(AssemblyIsolationPlan plan)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));

        switch (plan.Lifecycle)
        {
            case AssemblyIsolationLifecycle.Collectible:
#if NET
                collectibleContext = new CollectibleAssemblyIsolationContext(plan);
                collectibleContextReference = new WeakReference(collectibleContext);
                isCollectible = true;
                break;
#else
                throw new PlatformNotSupportedException("Collectible assembly isolation requires .NET Core or later.");
#endif
            case AssemblyIsolationLifecycle.ScopedNetFramework:
                netFrameworkScope = new NetFrameworkAssemblyIsolationScope(plan);
                break;
            case AssemblyIsolationLifecycle.Permanent:
                throw new NotSupportedException("Permanent assembly isolation is provided by the permanent loading session.");
            default:
                throw new ArgumentOutOfRangeException(nameof(plan));
        }
    }

    public static AssemblyIsolationSession Create(AssemblyIsolationPlan plan) => new(plan);

    public Assembly LoadEntryAssembly()
    {
        ThrowIfDisposed();
#if NET
        if (collectibleContext is not null)
            return collectibleContext.LoadEntryAssembly();
#endif
        return netFrameworkScope!.LoadEntryAssembly();
    }

    public Assembly LoadAssembly(byte[] assemblyBytes)
    {
        ThrowIfDisposed();
        if (assemblyBytes is null) throw new ArgumentNullException(nameof(assemblyBytes));
#if NET
        if (collectibleContext is not null)
            return collectibleContext.LoadAssembly(assemblyBytes);
#endif
        return netFrameworkScope!.LoadAssembly(assemblyBytes);
    }

    public Assembly LoadFromPath(string path)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An assembly path is required.", nameof(path));
#if NET
        if (collectibleContext is not null)
            return AssemblyStreamLoader.Load(collectibleContext, path);
#endif
        return netFrameworkScope!.LoadFromPath(path);
    }

#if NET
    internal nint ResolveNativeForTesting(string unmanagedDllName)
    {
        ThrowIfDisposed();
        return collectibleContext?.ResolveNativeForTesting(unmanagedDllName)
            ?? throw new InvalidOperationException("Native resolution testing requires a collectible session.");
    }

    internal Assembly? ResolveManagedForTesting(AssemblyName assemblyName)
    {
        ThrowIfDisposed();
        if (collectibleContext is null)
            throw new InvalidOperationException("Managed resolution testing requires a collectible session.");

        return collectibleContext.ResolveManagedForTesting(assemblyName);
    }
#endif

    public AssemblyUnloadResult VerifyUnload()
    {
#if NET
        if (!isCollectible)
            return new AssemblyUnloadResult(false, false, "Assemblies loaded into the default AppDomain cannot be individually unloaded.");

        Dispose();
        return AwaitUnload(collectibleContextReference!);
#else
        return new AssemblyUnloadResult(false, false, "Collectible assembly isolation is not available on .NET Framework.");
#endif
    }

#if NET
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static AssemblyUnloadResult AwaitUnload(WeakReference contextReference)
    {
        for (var attempt = 0; attempt < 10 && contextReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return contextReference.IsAlive
            ? new AssemblyUnloadResult(true, false, "The collectible AssemblyLoadContext is still reachable after bounded GC attempts.")
            : new AssemblyUnloadResult(true, true, null);
    }
#endif

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        netFrameworkScope?.Dispose();
        netFrameworkScope = null;
#if NET
        if (collectibleContext is not null)
        {
            collectibleContext.Unload();
            collectibleContext = null;
        }
#endif
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(AssemblyIsolationSession));
    }
}
