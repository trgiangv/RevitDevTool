using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Runtime;
using System.Reflection;
#if NET
using System.Runtime.CompilerServices;
using DevTools.AssemblyIsolation.Loading;
#endif

namespace DevTools.AssemblyIsolation;

public sealed class AssemblyIsolationSession : IDisposable
{
#if NET
    private AssemblyIsolationContext? _context;
    private readonly WeakReference? _contextReference;
#else
    private NetfxAssemblyIsolationContext? _netfxContext;
#endif
    private bool _disposed;

    private AssemblyIsolationSession(AssemblyIsolationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        switch (plan.Kind)
        {
            case AssemblyIsolationKind.Isolated:
            case AssemblyIsolationKind.Collectible:
#if NET
                _context = new AssemblyIsolationContext(plan);
                _contextReference = new WeakReference(_context);
                break;
#else
                if (plan.Kind == AssemblyIsolationKind.Collectible)
                    throw new PlatformNotSupportedException("Collectible assembly isolation requires .NET Core or later.");
                _netfxContext = new NetfxAssemblyIsolationContext(plan);
                break;
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
        return _context!.LoadEntryAssembly();
#else
        return _netfxContext!.LoadEntryAssembly();
#endif
    }

    public Assembly LoadAssembly(byte[] assemblyBytes, byte[]? symbolBytes = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(assemblyBytes);
#if NET
        return _context!.LoadAssembly(assemblyBytes, symbolBytes);
#else
        return _netfxContext!.LoadAssembly(assemblyBytes, symbolBytes);
#endif
    }

    public Assembly LoadFromPath(string path)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
#if NET
        return AssemblyStreamLoader.Load(_context!, path);
#else
        return _netfxContext!.LoadFromPath(path);
#endif
    }

#if NET
    internal nint ResolveNativeForTesting(string name)
    {
        ThrowIfDisposed();
        return _context!.ResolveNativeForTesting(name);
    }

    internal Assembly? ResolveManagedForTesting(AssemblyName assemblyName)
    {
        ThrowIfDisposed();
        return _context!.ResolveManagedForTesting(assemblyName);
    }
#endif

    public AssemblyUnloadResult VerifyUnload()
    {
#if NET
        Dispose();
        return AwaitUnload(_contextReference!);
#else
        Dispose();
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
        if (_disposed)
            return;

        _disposed = true;
#if NET
        if (_context is not null)
        {
            _context.Unload();
            _context = null;
        }
#else
        _netfxContext?.Dispose();
        _netfxContext = null;
#endif
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
