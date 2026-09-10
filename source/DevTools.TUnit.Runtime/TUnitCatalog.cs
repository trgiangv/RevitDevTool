using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Loading;
using ReflectionAssembly = System.Reflection.Assembly;

namespace DevTools.TUnit.Runtime;

internal static class TUnitCatalog
{
    public static IReadOnlyList<TestingDiscoveredTest> Discover(
        string assemblyPath,
        TestingSelection selection,
        ReflectionAssembly? alreadyLoaded = null) =>
        Enumerate(assemblyPath, selection, "discovery", alreadyLoaded);

    private static IReadOnlyList<TestingDiscoveredTest> Enumerate(
        string assemblyPath,
        TestingSelection selection,
        string sessionId,
        ReflectionAssembly? alreadyLoaded)
    {
        EnsureLoaded(assemblyPath, alreadyLoaded);
        var ids = selection.Kind == TestingSelectionKind.TestIds ? Clean(selection.TestIds) : [];
        var names = selection.Kind == TestingSelectionKind.Names ? Clean(selection.Names) : [];
        var tests = EnumerateMatches(sessionId, ids, names, matchAll: selection.Kind == TestingSelectionKind.All).ToList();
        if (tests.Count == 0 && ShouldReportEmptyRegistrar(selection))
        {
            throw new HostTestDiscoveryFailedException(
                "TUnit discovery found no SourceRegistrar entries. " +
                "The test assembly module constructor did not register TestEntry sources.");
        }

        return tests;
    }

    private static IEnumerable<TestingDiscoveredTest> EnumerateMatches(
        string sessionId,
        HashSet<string> ids,
        HashSet<string> names,
        bool matchAll)
    {
        foreach (var source in Sources.TestEntries.Values)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var filter = source.GetFilterData(index);
                var expansion = TUnitExpansion.Expand(source, index, sessionId);
                foreach (var combination in expansion.Combinations)
                {
                    var discovered = Map(source, filter, expansion.Metadata, combination);
                    if (MatchesSelection(discovered, filter, expansion.Metadata, ids, names, matchAll))
                        yield return discovered;
                }
            }
        }
    }

    private static bool ShouldReportEmptyRegistrar(TestingSelection selection) =>
        selection.Kind == TestingSelectionKind.All && Sources.TestEntries.IsEmpty;

    private static TestingDiscoveredTest Map(
        ITestEntrySource source,
        TestEntryFilterData filter,
        TestMetadata? metadata,
        TUnitCombination combination)
    {
        var namespaceName = metadata?.MethodMetadata.Class.Namespace ?? source.ClassType.Namespace ?? string.Empty;
        var typeName = metadata is not null
            ? TUnitTestIdentity.TypeNameWithGenerics(metadata.TestClassType)
            : filter.ClassName;
        var methodName = metadata?.TestMethodName ?? filter.MethodName;
        var className = string.IsNullOrEmpty(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
        var testId = combination.Deferred
            ? metadata is not null
                ? TUnitTestIdentity.Deferred(metadata)
                : TUnitTestIdentity.Fallback(namespaceName, typeName, methodName)
            : metadata is not null
                ? TUnitTestIdentity.From(metadata, combination)
                : TUnitTestIdentity.Fallback(namespaceName, typeName, methodName);
        var displayName = combination.DisplayName ?? FormatDisplay(methodName, combination);
        var sourceLocation = metadata is { FilePath.Length: > 0 }
            ? new TestingSourceLocation(metadata.FilePath, metadata.LineNumber)
            : null;

        return new TestingDiscoveredTest(
            testId,
            displayName,
            $"{className}.{methodName}",
            className,
            methodName,
            sourceLocation,
            namespaceName,
            typeName,
            MethodArity: metadata?.GenericMethodTypeArguments?.Length ?? 0,
            Categories: filter.Categories);
    }

    private static string FormatDisplay(string methodName, TUnitCombination combination)
    {
        if (combination.Deferred)
            return methodName;
        var parts = new List<string>();
        if (combination.MethodArgs.Length > 0)
            parts.Add(string.Join(", ", combination.MethodArgs.Select(FormatArg)));
        if (combination.Properties.Length > 0)
            parts.AddRange(combination.Properties.Select(property => $"{property.Name}={FormatArg(property.Value)}"));
        var suffix = combination.Indices.RepeatIndex > 0
            ? $" repeat {combination.Indices.RepeatIndex}"
            : string.Empty;
        return parts.Count == 0
            ? $"{methodName}{suffix}"
            : $"{methodName}({string.Join(", ", parts)}){suffix}";
    }

    private static string FormatArg(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "?",
    };

    private static void EnsureLoaded(string assemblyPath, ReflectionAssembly? alreadyLoaded)
    {
        SourceRegistrar.IsEnabled = true;
        if (alreadyLoaded is not null)
        {
            Bind(alreadyLoaded);
            return;
        }

        assemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(assemblyPath))
            throw new HostTestDiscoveryFailedException($"TUnit test assembly not found: {assemblyPath}");

        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            !assembly.IsDynamic
            && assembly.Location.Length > 0
            && string.Equals(Path.GetFullPath(assembly.Location), assemblyPath, StringComparison.OrdinalIgnoreCase));
        if (loaded is not null)
        {
            Bind(loaded);
            return;
        }

        var entry = ReflectionAssembly.GetEntryAssembly();
        if (entry is not null
            && entry.Location.Length > 0
            && string.Equals(Path.GetFullPath(entry.Location), assemblyPath, StringComparison.OrdinalIgnoreCase)
            && DiscoveryRefs.Read(assemblyPath).Count == 0)
        {
            Bind(entry);
            return;
        }

        using var load = DiscoveryAssemblyLoad.Open(assemblyPath);
        Bind(load.Assembly);
    }

    private static void Bind(ReflectionAssembly testAssembly)
    {
        RuntimeHelpers.RunModuleConstructor(testAssembly.ManifestModule.ModuleHandle);
        TUnitSourceCatalog.Retain(testAssembly);
    }

    private static bool MatchesSelection(
        TestingDiscoveredTest test,
        TestEntryFilterData filter,
        TestMetadata? metadata,
        HashSet<string> ids,
        HashSet<string> names,
        bool matchAll)
    {
        if (matchAll)
            return true;

        if (ids.Count == 0 && names.Count == 0)
            return false;

        if (ids.Contains(test.TestId)
            || (!string.IsNullOrWhiteSpace(test.FullName) && ids.Contains(test.FullName!)))
        {
            return true;
        }

        if (metadata is not null && ids.Contains(TUnitTestIdentity.Deferred(metadata)))
            return true;

        return names.Any(name =>
            string.Equals(test.FullName, name, StringComparison.Ordinal)
            || string.Equals(test.DisplayName, name, StringComparison.Ordinal)
            || (test.FullName?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false)
            || test.TestId.Contains(name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(filter.MethodName, name, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> Clean(IReadOnlyList<string>? values) =>
        values is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.Ordinal);
}
