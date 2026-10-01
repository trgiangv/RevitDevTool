using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.MTP;

/// <summary>
/// MSTest <see cref="DependsOnAttribute"/> orders tests inside one run; it does not
/// add prerequisites to a filtered run. Running one test by id would execute it with
/// none of its prerequisites. This closes a selection over the declared edges so the
/// host runs the prerequisites too (TUnit's engine does the same for its DependsOn).
/// </summary>
internal static class MSTestExpansion
{
    private const BindingFlags Members =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    public static IReadOnlyList<string> Expand(
        Assembly assembly,
        IReadOnlyList<TestDiscoveredTest> all,
        IEnumerable<string> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(selectedIds);

        var byId = new Dictionary<string, TestDiscoveredTest>(StringComparer.Ordinal);
        foreach (var test in all)
            byId.TryAdd(test.TestId, test);

        var types = IndexTypes(assembly);
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        foreach (var id in selectedIds)
        {
            if (seen.Add(id))
            {
                ordered.Add(id);
                pending.Enqueue(id);
            }
        }

        while (pending.Count > 0)
        {
            if (!byId.TryGetValue(pending.Dequeue(), out var test))
                continue;

            foreach (var prerequisite in Prerequisites(test, all, types))
            {
                if (!seen.Add(prerequisite.TestId))
                    continue;

                ordered.Add(prerequisite.TestId);
                pending.Enqueue(prerequisite.TestId);
            }
        }

        return ordered;
    }

    private static IEnumerable<TestDiscoveredTest> Prerequisites(
        TestDiscoveredTest test,
        IReadOnlyList<TestDiscoveredTest> all,
        Dictionary<string, Type> types)
    {
        if (test.ClassName is null || test.MethodName is null || !types.TryGetValue(Normalize(test.ClassName), out var type))
            yield break;

        var edges = new List<DependsOnAttribute>();
        edges.AddRange(type.GetCustomAttributes(typeof(DependsOnAttribute), inherit: false).OfType<DependsOnAttribute>());
        foreach (var method in type.GetMethods(Members).Where(method => method.Name == test.MethodName))
            edges.AddRange(method.GetCustomAttributes(typeof(DependsOnAttribute), inherit: false).OfType<DependsOnAttribute>());

        foreach (var edge in edges)
        {
            var targetClass = Normalize((edge.TestClass ?? type).FullName ?? string.Empty);
            foreach (var candidate in all)
            {
                if (candidate.ClassName is null
                    || !string.Equals(Normalize(candidate.ClassName), targetClass, StringComparison.Ordinal))
                {
                    continue;
                }

                if (edge.TestMethodName is not null
                    && !string.Equals(candidate.MethodName, edge.TestMethodName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(candidate.TestId, test.TestId, StringComparison.Ordinal))
                    yield return candidate;
            }
        }
    }

    private static Dictionary<string, Type> IndexTypes(Assembly assembly)
    {
        Type[] loadable;
        try
        {
            loadable = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            loadable = ex.Types.Where(type => type is not null).ToArray()!;
        }

        var index = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in loadable)
        {
            if (type.FullName is { } name)
                index.TryAdd(Normalize(name), type);
        }

        return index;
    }

    private static string Normalize(string typeName) => typeName.Replace('+', '.');
}