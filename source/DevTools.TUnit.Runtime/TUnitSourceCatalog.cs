using System.Collections.Concurrent;
using System.Reflection;
using DevTools.Testing.Abstractions.Runtime;
using TUnit.Core.Hooks;

namespace DevTools.TUnit.Runtime;

/// <summary>
/// TUnit.Core <see cref="Sources"/> is process-wide. On net48 (and whenever
/// <c>TUnit.Core</c> is identity-bound across generations) each rebuild
/// <c>LoadFile</c>s a new test assembly whose module constructor <b>adds</b>
/// entries. Old assemblies cannot unload, so Engine would run every historical
/// copy of the same UID and concatenate their Console output.
/// </summary>
/// <remarks>
/// Entries for other assemblies are parked, not discarded. Reverting a source
/// edit reuses the previous generation hash and the already-loaded assembly;
/// the module constructor is then a no-op, so parked sources must be restored
/// or Engine reports no result for the UID.
/// <para>
/// Parked maps live on <see cref="TestingProcessHold"/> (parent-bound
/// Abstractions), not on Runtime statics. net48 <c>LoadFile</c>s a distinct
/// Runtime copy from each generation shadow folder while TUnit.Core stays
/// identity-bound — a Runtime-local park bag is invisible when the previous
/// generation session is recreated.
/// </para>
/// </remarks>
internal static class TUnitSourceCatalog
{
    private const string HoldSlot = "tunit.sources";

    public static void Retain(Assembly testAssembly)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);

        lock (TestingProcessHold.Gate)
        {
            Activate(Sources.TestEntries, Map<Type, ITestEntrySource>("TestEntries"), testAssembly, static type => type.Assembly);
            Activate(Sources.BeforeTestHooks, Map<Type, ConcurrentBag<LazyHookEntry<InstanceHookMethod>>>("BeforeTestHooks"), testAssembly, static type => type.Assembly);
            Activate(Sources.AfterTestHooks, Map<Type, ConcurrentBag<LazyHookEntry<InstanceHookMethod>>>("AfterTestHooks"), testAssembly, static type => type.Assembly);
            Activate(Sources.BeforeClassHooks, Map<Type, ConcurrentBag<LazyHookEntry<BeforeClassHookMethod>>>("BeforeClassHooks"), testAssembly, static type => type.Assembly);
            Activate(Sources.AfterClassHooks, Map<Type, ConcurrentBag<LazyHookEntry<AfterClassHookMethod>>>("AfterClassHooks"), testAssembly, static type => type.Assembly);
            Activate(Sources.BeforeAssemblyHooks, Map<Assembly, ConcurrentBag<LazyHookEntry<BeforeAssemblyHookMethod>>>("BeforeAssemblyHooks"), testAssembly, static assembly => assembly);
            Activate(Sources.AfterAssemblyHooks, Map<Assembly, ConcurrentBag<LazyHookEntry<AfterAssemblyHookMethod>>>("AfterAssemblyHooks"), testAssembly, static assembly => assembly);
            ActivateBag(Sources.BeforeEveryTestHooks, ListMap<LazyHookEntry<BeforeTestHookMethod>>("BeforeEveryTestHooks"), testAssembly);
            ActivateBag(Sources.AfterEveryTestHooks, ListMap<LazyHookEntry<AfterTestHookMethod>>("AfterEveryTestHooks"), testAssembly);
            ActivateBag(Sources.BeforeEveryClassHooks, ListMap<LazyHookEntry<BeforeClassHookMethod>>("BeforeEveryClassHooks"), testAssembly);
            ActivateBag(Sources.AfterEveryClassHooks, ListMap<LazyHookEntry<AfterClassHookMethod>>("AfterEveryClassHooks"), testAssembly);
            ActivateBag(Sources.BeforeEveryAssemblyHooks, ListMap<LazyHookEntry<BeforeAssemblyHookMethod>>("BeforeEveryAssemblyHooks"), testAssembly);
            ActivateBag(Sources.AfterEveryAssemblyHooks, ListMap<LazyHookEntry<AfterAssemblyHookMethod>>("AfterEveryAssemblyHooks"), testAssembly);
            ActivateBag(Sources.BeforeTestSessionHooks, ListMap<LazyHookEntry<BeforeTestSessionHookMethod>>("BeforeTestSessionHooks"), testAssembly);
            ActivateBag(Sources.AfterTestSessionHooks, ListMap<LazyHookEntry<AfterTestSessionHookMethod>>("AfterTestSessionHooks"), testAssembly);
            ActivateBag(Sources.BeforeTestDiscoveryHooks, ListMap<LazyHookEntry<BeforeTestDiscoveryHookMethod>>("BeforeTestDiscoveryHooks"), testAssembly);
            ActivateBag(Sources.AfterTestDiscoveryHooks, ListMap<LazyHookEntry<AfterTestDiscoveryHookMethod>>("AfterTestDiscoveryHooks"), testAssembly);
            Drain(Sources.AssemblyLoaders);
        }
    }

    private static Dictionary<Assembly, Dictionary<TKey, TValue>> Map<TKey, TValue>(string name)
        where TKey : notnull =>
        Slot(name, static () => new Dictionary<Assembly, Dictionary<TKey, TValue>>());

    private static Dictionary<Assembly, List<T>> ListMap<T>(string name) =>
        Slot(name, static () => new Dictionary<Assembly, List<T>>());

    private static T Slot<T>(string name, Func<T> create) where T : class
    {
        var bags = TestingProcessHold.GetOrAdd(
            HoldSlot,
            static () => new Dictionary<string, object>(StringComparer.Ordinal));
        if (bags.TryGetValue(name, out var existing) && existing is T typed)
            return typed;
        if (existing is not null)
            return create();

        var created = create();
        bags[name] = created;
        return created;
    }

    private static void Activate<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> live,
        Dictionary<Assembly, Dictionary<TKey, TValue>> parked,
        Assembly current,
        Func<TKey, Assembly> owner)
        where TKey : notnull
    {
        foreach (var pair in live)
        {
            var assembly = owner(pair.Key);
            if (ReferenceEquals(assembly, current))
                continue;

            Stash(parked, assembly)[pair.Key] = pair.Value;
            live.TryRemove(pair.Key, out _);
        }

        if (!parked.TryGetValue(current, out var stash))
            return;

        foreach (var pair in stash)
            live.TryAdd(pair.Key, pair.Value);

        parked.Remove(current);
    }

    private static void ActivateBag<T>(
        ConcurrentBag<LazyHookEntry<T>> live,
        Dictionary<Assembly, List<LazyHookEntry<T>>> parked,
        Assembly current)
        where T : HookMethod
    {
        var keep = new List<LazyHookEntry<T>>();
        while (live.TryTake(out var entry))
        {
            Assembly? owner;
            try
            {
                var hook = entry.Materialize();
                owner = hook.Assembly ?? hook.ClassType.Assembly;
            }
            catch
            {
                continue;
            }

            if (ReferenceEquals(owner, current))
                keep.Add(entry);
            else
                Stash(parked, owner).Add(entry);
        }

        if (parked.TryGetValue(current, out var stash))
        {
            keep.AddRange(stash);
            parked.Remove(current);
        }

        foreach (var entry in keep)
            live.Add(entry);
    }

    private static Dictionary<TKey, TValue> Stash<TKey, TValue>(
        Dictionary<Assembly, Dictionary<TKey, TValue>> parked,
        Assembly assembly)
        where TKey : notnull
    {
        if (!parked.TryGetValue(assembly, out var stash))
        {
            stash = new Dictionary<TKey, TValue>();
            parked[assembly] = stash;
        }

        return stash;
    }

    private static List<T> Stash<T>(Dictionary<Assembly, List<T>> parked, Assembly assembly)
    {
        if (parked.TryGetValue(assembly, out var stash)) return stash;
        stash = [];
        parked[assembly] = stash;

        return stash;
    }

    private static void Drain<T>(ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out _))
        {
        }
    }
}
