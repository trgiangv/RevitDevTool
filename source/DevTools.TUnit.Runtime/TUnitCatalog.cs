using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using TUnit.Core;
using ReflectionAssembly = System.Reflection.Assembly;

namespace DevTools.TUnit.Runtime;

internal static class TUnitCatalog
{
    public static IReadOnlyList<TestingDiscoveredTest> Discover(
        string assemblyPath,
        TestingSelection selection,
        TestingDiscoveryOptions options,
        ReflectionAssembly? alreadyLoaded = null)
    {
        return Enumerate(assemblyPath, selection, options, "discovery", alreadyLoaded).ToList();
    }

    static IEnumerable<TestingDiscoveredTest> Enumerate(
        string assemblyPath,
        TestingSelection selection,
        TestingDiscoveryOptions options,
        string sessionId,
        ReflectionAssembly? alreadyLoaded)
    {
        EnsureLoaded(assemblyPath, alreadyLoaded);
        var ids = Clean(selection.TestIds);
        var names = Clean(selection.Names);
        var hints = selection.Hints;
        var yielded = false;

        foreach (var source in Sources.TestEntries.Values)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var filter = source.GetFilterData(index);
                if (!MatchesHints(source, filter, hints))
                    continue;

                foreach (var combination in TUnitExpansion.Expand(source, index, sessionId, options))
                {
                    var discovered = Map(source, filter, combination, options);
                    if (!MatchesSelection(discovered, filter, combination, ids, names))
                        continue;
                    yielded = true;
                    yield return discovered;
                }
            }
        }

        if (!yielded
            && ids.Count == 0
            && names.Count == 0
            && (hints is null || hints.IsEmpty)
            && Sources.TestEntries.IsEmpty)
        {
            throw new HostTestDiscoveryFailedException(
                "TUnit discovery found no SourceRegistrar entries. " +
                "The test assembly module constructor did not register TestEntry sources.");
        }
    }

    static TestingDiscoveredTest Map(
        ITestEntrySource source,
        TestEntryFilterData filter,
        TUnitCombination combination,
        TestingDiscoveryOptions options)
    {
        var metadata = combination.Metadata;
        var namespaceName = metadata?.MethodMetadata.Class.Namespace ?? source.ClassType.Namespace ?? string.Empty;
        var typeName = metadata is not null
            ? TUnitTestIdentity.TypeNameWithGenerics(metadata.TestClassType)
            : filter.ClassName;
        var methodName = metadata?.TestMethodName ?? filter.MethodName;
        var className = string.IsNullOrEmpty(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
        var testId = combination.Deferred && !options.ForExecution
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
            HasDataSource: combination.Deferred,
            Categories: filter.Categories);
    }

    static string FormatDisplay(string methodName, TUnitCombination combination)
    {
        if (combination.Deferred)
            return methodName;
        var parts = new List<string>();
        if (combination.MethodArgs.Length > 0)
            parts.Add(string.Join(", ", combination.MethodArgs.Select(FormatArg)));
        if (combination.Properties.Length > 0)
            parts.AddRange(combination.Properties.Select(property => $"{property.Name}={FormatArg(property.Value)}"));
        var suffix = combination.RepeatIndex > 0 ? $" repeat {combination.RepeatIndex}" : string.Empty;
        return parts.Count == 0
            ? $"{methodName}{suffix}"
            : $"{methodName}({string.Join(", ", parts)}){suffix}";
    }

    static string FormatArg(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "?",
    };

    static void EnsureLoaded(string assemblyPath, ReflectionAssembly? alreadyLoaded)
    {
        SourceRegistrar.IsEnabled = true;
        if (alreadyLoaded is not null)
        {
            RuntimeHelpers.RunModuleConstructor(alreadyLoaded.ManifestModule.ModuleHandle);
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
            RuntimeHelpers.RunModuleConstructor(loaded.ManifestModule.ModuleHandle);
            return;
        }

        var entry = ReflectionAssembly.GetEntryAssembly();
        if (entry is not null
            && entry.Location.Length > 0
            && string.Equals(Path.GetFullPath(entry.Location), assemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            RuntimeHelpers.RunModuleConstructor(entry.ManifestModule.ModuleHandle);
            return;
        }

        RuntimeHelpers.RunModuleConstructor(ReflectionAssembly.LoadFrom(assemblyPath).ManifestModule.ModuleHandle);
    }

    static bool MatchesHints(ITestEntrySource source, TestEntryFilterData filter, TestingDiscoveryHints? hints)
    {
        if (hints is null || hints.IsEmpty)
            return true;

        if (!IsBlank(hints.ClassNames)
            && !hints.ClassNames!.Any(name =>
                string.Equals(name, filter.ClassName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, source.ClassType.Name, StringComparison.OrdinalIgnoreCase)
                || (source.ClassType.FullName?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false)))
        {
            return false;
        }

        if (!IsBlank(hints.MethodNames)
            && !hints.MethodNames!.Any(name =>
                string.Equals(name, filter.MethodName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!IsBlank(hints.Categories)
            && !filter.Categories.Any(category =>
                hints.Categories!.Contains(category, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    static bool MatchesSelection(
        TestingDiscoveredTest test,
        TestEntryFilterData filter,
        TUnitCombination combination,
        HashSet<string> ids,
        HashSet<string> names)
    {
        if (ids.Count == 0 && names.Count == 0)
            return true;

        if (ids.Contains(test.TestId)
            || (!string.IsNullOrWhiteSpace(test.FullName) && ids.Contains(test.FullName!)))
        {
            return true;
        }

        if (combination.Metadata is not null && ids.Contains(TUnitTestIdentity.Deferred(combination.Metadata)))
            return true;

        return names.Any(name =>
            string.Equals(test.FullName, name, StringComparison.Ordinal)
            || string.Equals(test.DisplayName, name, StringComparison.Ordinal)
            || (test.FullName?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false)
            || test.TestId.Contains(name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(filter.MethodName, name, StringComparison.OrdinalIgnoreCase));
    }

    static HashSet<string> Clean(IReadOnlyList<string>? values) =>
        values is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.Ordinal);

    static bool IsBlank(IReadOnlyList<string>? values) => values is null || values.Count == 0;
}
