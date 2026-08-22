using System.Collections.Concurrent;
using System.Reflection;
using TUnit.Core.Hooks;

namespace DevTools.TUnit.Runtime;

/// <summary>
/// TUnit.Core <see cref="Sources"/> is process-wide. On net48 (and whenever
/// <c>TUnit.Core</c> is identity-bound across generations) each rebuild
/// <c>LoadFile</c>s a new test assembly whose module constructor <b>adds</b>
/// entries. Old assemblies cannot unload, so Engine would run every historical
/// copy of the same UID and concatenate their Console output.
/// </summary>
internal static class TUnitSourceCatalog
{
    private static readonly object Sync = new();

    public static void Retain(Assembly testAssembly)
    {
        if (testAssembly is null)
            throw new ArgumentNullException(nameof(testAssembly));

        lock (Sync)
        {
            Prune(Sources.TestEntries, type => BelongsTo(type, testAssembly));
            Prune(Sources.BeforeTestHooks, type => BelongsTo(type, testAssembly));
            Prune(Sources.AfterTestHooks, type => BelongsTo(type, testAssembly));
            Prune(Sources.BeforeClassHooks, type => BelongsTo(type, testAssembly));
            Prune(Sources.AfterClassHooks, type => BelongsTo(type, testAssembly));
            Prune(Sources.BeforeAssemblyHooks, assembly => ReferenceEquals(assembly, testAssembly));
            Prune(Sources.AfterAssemblyHooks, assembly => ReferenceEquals(assembly, testAssembly));
            RetainBag(Sources.BeforeEveryTestHooks, testAssembly);
            RetainBag(Sources.AfterEveryTestHooks, testAssembly);
            RetainBag(Sources.BeforeEveryClassHooks, testAssembly);
            RetainBag(Sources.AfterEveryClassHooks, testAssembly);
            RetainBag(Sources.BeforeEveryAssemblyHooks, testAssembly);
            RetainBag(Sources.AfterEveryAssemblyHooks, testAssembly);
            RetainBag(Sources.BeforeTestSessionHooks, testAssembly);
            RetainBag(Sources.AfterTestSessionHooks, testAssembly);
            RetainBag(Sources.BeforeTestDiscoveryHooks, testAssembly);
            RetainBag(Sources.AfterTestDiscoveryHooks, testAssembly);
            Drain(Sources.AssemblyLoaders);
        }
    }

    private static bool BelongsTo(Type type, Assembly testAssembly) =>
        ReferenceEquals(type.Assembly, testAssembly);

    private static void Prune<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> map,
        Func<TKey, bool> keep)
        where TKey : notnull
    {
        foreach (var key in map.Keys)
        {
            if (!keep(key))
                map.TryRemove(key, out _);
        }
    }

    private static void RetainBag<T>(ConcurrentBag<LazyHookEntry<T>> bag, Assembly testAssembly)
        where T : HookMethod
    {
        var keep = new List<LazyHookEntry<T>>();
        while (bag.TryTake(out var entry))
        {
            try
            {
                var hook = entry.Materialize();
                if (ReferenceEquals(hook.Assembly, testAssembly) || BelongsTo(hook.ClassType, testAssembly))
                    keep.Add(entry);
            }
            catch
            {
                // Retired generation hooks may fail to materialize; drop them.
            }
        }

        foreach (var entry in keep)
            bag.Add(entry);
    }

    private static void Drain<T>(ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out _))
        {
        }
    }
}
