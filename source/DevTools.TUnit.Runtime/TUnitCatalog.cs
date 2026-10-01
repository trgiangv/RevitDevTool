using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Loading;
using ReflectionAssembly = System.Reflection.Assembly;

namespace DevTools.TUnit.Runtime;

internal static class TUnitCatalog
{
    public static IReadOnlyList<TestDiscoveredTest> Discover(
        string assemblyPath,
        TestSelection selection) =>
        Enumerate(assemblyPath, selection, "discovery");

    private static List<TestDiscoveredTest> Enumerate(
        string assemblyPath,
        TestSelection selection,
        string sessionId)
    {
        EnsureLoaded(assemblyPath);
        var tests = EnumerateMatches(sessionId, TestSelectionMatcher.For(selection)).ToList();
        if (tests.Count == 0 && ShouldReportEmptyRegistrar(selection))
        {
            throw new TestingDiscoveryFailedException(
                "TUnit discovery found no SourceRegistrar entries. " +
                "The test assembly module constructor did not register TestEntry sources.");
        }

        return tests;
    }

    private static IEnumerable<TestDiscoveredTest> EnumerateMatches(
        string sessionId,
        TestSelectionMatcher matcher)
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
                    if (matcher.Matches(
                            discovered,
                            filter.MethodName,
                            () => expansion.Metadata is null ? null : TUnitTestIdentity.Deferred(expansion.Metadata)))
                        yield return discovered;
                }
            }
        }
    }

    private static bool ShouldReportEmptyRegistrar(TestSelection selection) =>
        selection.Kind == TestSelectionKind.All && Sources.TestEntries.IsEmpty;

    private static TestDiscoveredTest Map(
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
        var testId = TestId(combination, metadata, namespaceName, typeName, methodName);
        var displayName = combination.DisplayName ?? FormatDisplay(methodName, combination);
        var sourceLocation = metadata is { FilePath.Length: > 0 }
            ? new TestSourceLocation(metadata.FilePath, metadata.LineNumber)
            : null;
        var method = metadata?.MethodMetadata;
        var parameterTypes = method is null ? null : TUnitMetadataNames.Parameters(method.Parameters);
        var returnType = method is null ? null : TUnitMetadataNames.ReturnType(method);

        return new TestDiscoveredTest(
            testId,
            displayName,
            $"{className}.{methodName}",
            className,
            methodName,
            sourceLocation,
            namespaceName,
            typeName,
            MethodArity: method?.GenericTypeCount ?? 0,
            Categories: filter.Categories,
            ParameterTypeFullNames: parameterTypes,
            ReturnTypeFullName: returnType);
    }

    private static string TestId(
        TUnitCombination combination,
        TestMetadata? metadata,
        string namespaceName,
        string typeName,
        string methodName)
    {
        if (metadata is null)
            return TUnitTestIdentity.Fallback(namespaceName, typeName, methodName);

        return combination.Deferred
            ? TUnitTestIdentity.Deferred(metadata)
            : TUnitTestIdentity.From(metadata, combination);
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

    private static void EnsureLoaded(string assemblyPath)
    {
        SourceRegistrar.IsEnabled = true;
        assemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(assemblyPath))
            throw new TestingDiscoveryFailedException($"TUnit test assembly not found: {assemblyPath}");

        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            assembly is { IsDynamic: false, Location.Length: > 0 }
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
}
