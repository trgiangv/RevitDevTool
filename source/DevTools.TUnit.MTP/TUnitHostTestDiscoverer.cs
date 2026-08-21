using System.Collections;
using System.Reflection;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.TUnit.MTP;

internal sealed class TUnitHostTestDiscoverer : IHostTestDiscoverer
{
    public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath) =>
        Select(assemblyPath, new TestingSelection([]));

    public IReadOnlyList<TestingDiscoveredTest> Select(string assemblyPath, TestingSelection selection)
    {
        var all = DiscoverGeneratedEntries(assemblyPath);
        var ids = Clean(selection.TestIds);
        var names = Clean(selection.Names);
        if (ids.Count == 0 && names.Count == 0)
            return all;

        return all.Where(test =>
                ids.Contains(test.TestId)
                || (!string.IsNullOrWhiteSpace(test.FullName) && ids.Contains(test.FullName!))
                || names.Any(name => MatchesName(test, name)))
            .ToList();
    }

    public TestingSelection ToHostSelection(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        if (requested.TestIds.Count == 0 && (requested.Names?.Count ?? 0) == 0)
            return new TestingSelection([]);
        return new TestingSelection(discovered.Select(test => test.TestId).Distinct(StringComparer.Ordinal).ToList());
    }

    public IReadOnlyList<TestingCaseResult> FoldResults(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        _ = requested;
        _ = discovered;
        return hostResults;
    }

    public IReadOnlyList<TestingCaseResult> ResultsForUnreported(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        _ = requested;
        var reported = hostResults.Select(result => result.TestId).ToHashSet(StringComparer.Ordinal);
        return discovered
            .Where(test => !reported.Contains(test.TestId))
            .Select(test => new TestingCaseResult(
                test.TestId,
                test.DisplayName,
                TestingOutcomes.Error,
                0,
                "TUnit did not report a result for the selected test.",
                null,
                null,
                test.Source,
                [],
                [],
                FullName: test.FullName))
            .ToList();
    }

    private static IReadOnlyList<TestingDiscoveredTest> DiscoverGeneratedEntries(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(assemblyPath))
            return [];

        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null
            || !string.Equals(Path.GetFullPath(assembly.Location), assemblyPath, StringComparison.OrdinalIgnoreCase))
            assembly = Assembly.LoadFrom(assemblyPath);

        var tests = new List<TestingDiscoveredTest>();
        foreach (var type in assembly.GetTypes().Where(type =>
                     type.Namespace == "TUnit.Generated"
                     && type.Name.EndsWith("__TestSource", StringComparison.Ordinal)))
        {
            if (type.GetField("Entries", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is not IEnumerable entries)
                continue;

            foreach (var entry in entries)
            {
                if (entry is null)
                    continue;
                tests.Add(MapEntry(entry));
            }
        }

        return tests;
    }

    private static TestingDiscoveredTest MapEntry(object entry)
    {
        var type = entry.GetType();
        var fullName = Read<string>(type, entry, "FullyQualifiedName");
        var methodName = Read<string>(type, entry, "MethodName");
        var filePath = Read<string>(type, entry, "FilePath");
        var line = Read<int>(type, entry, "LineNumber");
        var className = fullName[..^(methodName.Length + 1)];
        var lastDot = className.LastIndexOf('.');
        var namespaceName = lastDot < 0 ? string.Empty : className[..lastDot];
        var typeName = lastDot < 0 ? className : className[(lastDot + 1)..];
        var uid = $"{className}.1.1.{methodName}.1.1.0";

        return new TestingDiscoveredTest(
            uid,
            methodName,
            fullName,
            className,
            methodName,
            string.IsNullOrWhiteSpace(filePath) ? null : new TestingSourceLocation(filePath, line),
            namespaceName,
            typeName);
    }

    private static T Read<T>(Type type, object instance, string name) =>
        (T)(type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance)
            ?? throw new HostTestDiscoveryFailedException($"TUnit generated entry is missing '{name}'."));

    private static HashSet<string> Clean(IReadOnlyList<string>? values) =>
        values is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.Ordinal);

    private static bool MatchesName(TestingDiscoveredTest test, string filter) =>
        string.Equals(test.FullName, filter, StringComparison.Ordinal)
        || string.Equals(test.DisplayName, filter, StringComparison.Ordinal)
        || (test.FullName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}
