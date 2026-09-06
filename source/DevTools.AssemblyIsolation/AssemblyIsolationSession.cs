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
    private AssemblyIsolationContext? context;
    private readonly WeakReference? contextReference;
#else
    private NetfxAssemblyIsolationContext? netfxContext;
#endif
    private bool disposed;

    private AssemblyIsolationSession(AssemblyIsolationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        switch (plan.Kind)
        {
            case AssemblyIsolationKind.Isolated:
#if NET
                context = new AssemblyIsolationContext(plan);
                contextReference = new WeakReference(context);
#else
                netfxContext = new NetfxAssemblyIsolationContext(plan);
#endif
                break;
            case AssemblyIsolationKind.Collectible:
#if NET
                context = new AssemblyIsolationContext(plan);
                contextReference = new WeakReference(context);
                break;
#else
                throw new PlatformNotSupportedException("Collectible assembly isolation requires .NET Core or later.");
#endif
            case AssemblyIsolationKind.Permanent:
                throw new NotSupportedException("Permanent assembly isolation is provided by AssemblyLoader, not AssemblyIsolationSession.");
            default:
                throw new ArgumentOutOfRangeException(nameof(plan));
        }
    }

    public static AssemblyIsolationSession Create(AssemblyIsolationPlan plan) => new(plan);

    public Assembly LoadEntryAssembly()
    {
        ThrowIfDisposed();
#if NET
        return context!.LoadEntryAssembly();
#else
        return netfxContext!.LoadEntryAssembly();
#endif
    }

    public Assembly LoadAssembly(byte[] assemblyBytes, byte[]? symbolBytes = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(assemblyBytes);
#if NET
        return context!.LoadAssembly(assemblyBytes, symbolBytes);
#else
        return netfxContext!.LoadAssembly(assemblyBytes, symbolBytes);
#endif
    }

    public Assembly LoadFromPath(string path)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
#if NET
        return AssemblyStreamLoader.Load(context!, path);
#else
        return netfxContext!.LoadFromPath(path);
#endif
    }

#if NET
    internal nint ResolveNativeForTesting(string name)
    {
        ThrowIfDisposed();
        return context!.ResolveNativeForTesting(name);
    }

    internal Assembly? ResolveManagedForTesting(AssemblyName assemblyName)
    {
        ThrowIfDisposed();
        return context!.ResolveManagedForTesting(assemblyName);
    }
#endif

    public AssemblyUnloadResult VerifyUnload()
    {
#if NET
        Dispose();
        return AwaitUnload(contextReference!);
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
#if NET
        if (context is not null)
        {
            context.Unload();
            context = null;
        }
#else
        netfxContext?.Dispose();
        netfxContext = null;
#endif
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
