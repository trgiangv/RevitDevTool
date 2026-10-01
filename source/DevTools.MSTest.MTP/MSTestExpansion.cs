using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;

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

        var byId = IndexById(all);
        var types = IndexTypes(assembly);
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        foreach (var id in selectedIds)
            Enqueue(id, ordered, seen, pending);

        while (pending.Count > 0)
            AppendPrerequisites(pending.Dequeue(), byId, all, types, ordered, seen, pending);

        return ordered;
    }

    private static Dictionary<string, TestDiscoveredTest> IndexById(IReadOnlyList<TestDiscoveredTest> all)
    {
        var byId = new Dictionary<string, TestDiscoveredTest>(StringComparer.Ordinal);
        foreach (var test in all)
            byId.TryAdd(test.TestId, test);

        return byId;
    }

    private static void Enqueue(
        string id,
        List<string> ordered,
        HashSet<string> seen,
        Queue<string> pending)
    {
        if (!seen.Add(id))
            return;

        ordered.Add(id);
        pending.Enqueue(id);
    }

    private static void AppendPrerequisites(
        string id,
        Dictionary<string, TestDiscoveredTest> byId,
        IReadOnlyList<TestDiscoveredTest> all,
        Dictionary<string, Type> types,
        List<string> ordered,
        HashSet<string> seen,
        Queue<string> pending)
    {
        if (!byId.TryGetValue(id, out var test))
            return;

        foreach (var prerequisite in Prerequisites(test, all, types))
            Enqueue(prerequisite.TestId, ordered, seen, pending);
    }

    private static IEnumerable<TestDiscoveredTest> Prerequisites(
        TestDiscoveredTest test,
        IReadOnlyList<TestDiscoveredTest> all,
        Dictionary<string, Type> types)
    {
        if (!TryResolve(test, types, out var type))
            yield break;

        foreach (var edge in Edges(type, test.MethodName!))
        {
            foreach (var candidate in Matches(all, test, type, edge))
                yield return candidate;
        }
    }

    private static bool TryResolve(
        TestDiscoveredTest test,
        Dictionary<string, Type> types,
        out Type type)
    {
        if (test.ClassName is not null
            && test.MethodName is not null
            && types.TryGetValue(Normalize(test.ClassName), out type!))
            return true;

        type = null!;
        return false;
    }

    private static IEnumerable<DependsOnAttribute> Edges(Type type, string methodName)
    {
        foreach (var attribute in Attributes(type))
            yield return attribute;

        foreach (var method in type.GetMethods(Members))
        {
            if (method.Name != methodName)
                continue;

            foreach (var attribute in Attributes(method))
                yield return attribute;
        }
    }

    private static IEnumerable<DependsOnAttribute> Attributes(MemberInfo member) =>
        member.GetCustomAttributes(typeof(DependsOnAttribute), inherit: false).OfType<DependsOnAttribute>();

    private static IEnumerable<TestDiscoveredTest> Matches(
        IReadOnlyList<TestDiscoveredTest> all,
        TestDiscoveredTest test,
        Type type,
        DependsOnAttribute edge)
    {
        var targetClass = Normalize((edge.TestClass ?? type).FullName ?? string.Empty);
        foreach (var candidate in all)
        {
            if (IsPrerequisite(candidate, test, targetClass, edge))
                yield return candidate;
        }
    }

    private static bool IsPrerequisite(
        TestDiscoveredTest candidate,
        TestDiscoveredTest test,
        string targetClass,
        DependsOnAttribute edge)
    {
        if (candidate.ClassName is null)
            return false;

        if (!string.Equals(Normalize(candidate.ClassName), targetClass, StringComparison.Ordinal))
            return false;

        if (edge.TestMethodName is not null
            && !string.Equals(candidate.MethodName, edge.TestMethodName, StringComparison.Ordinal))
            return false;

        return !string.Equals(candidate.TestId, test.TestId, StringComparison.Ordinal);
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
            // ReSharper disable once RedundantSuppressNullableWarningExpression
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