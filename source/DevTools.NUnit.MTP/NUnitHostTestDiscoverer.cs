using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using NUnit;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Authoritative local discovery via <see cref="NUnitTestAssemblyRunner"/>.
/// Test ids are MTP uids (`Class.Method` or `Class.Method("TestName")` for
/// renamed leaves). Host <c>&lt;test&gt;</c> still uses NUnit
/// <see cref="ITest.FullName"/>. No host launch.
/// </summary>
internal sealed class NUnitHostTestDiscoverer : IHostTestDiscoverer
{
    public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath) =>
        Select(assemblyPath, new TestingSelection([]));

    public IReadOnlyList<TestingDiscoveredTest> Select(
        string assemblyPath,
        TestingSelection selection)
    {
        using var session = NUnitLocalExploration.Load(assemblyPath);
        var all = session.Leaves.Select(test => ToDiscovered(test, session.Source)).ToList();
        var testIds = CleanIds(selection.TestIds);
        var names = CleanIds(selection.Names);
        if (testIds.Count == 0 && names.Count == 0)
            return all;

        var selected = new List<TestingDiscoveredTest>();
        if (testIds.Count > 0)
        {
            selected.AddRange(all.Where(test => testIds.Any(id =>
                string.Equals(id, test.TestId, StringComparison.Ordinal)
                || string.Equals(id, test.FullName, StringComparison.Ordinal)
                || test.TestId.StartsWith(id + "(", StringComparison.Ordinal)
                || NUnitCollapsedSelection.Matches(id, test.TestId, test.FullName, null))));
        }

        if (names.Count > 0)
        {
            var xml = NUnitSelectionXml.ToFilterXml(names);
            var filter = NUnitFilterXml.Create(xml);
            var nameHits = new HashSet<string>(
                session.Leaves.Where(filter.Pass).Select(test => test.FullName),
                StringComparer.Ordinal);
            selected.AddRange(all.Where(test =>
                !string.IsNullOrWhiteSpace(test.FullName) && nameHits.Contains(test.FullName!)));
        }

        return selected
            .GroupBy(test => test.TestId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static List<string> CleanIds(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return [];

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static TestingDiscoveredTest ToDiscovered(ITest test, NUnitSourceLocationProvider? source)
    {
        NUnitTestNameParser.SplitIde(test.FullName, out var namespaceName, out var typeName, out var parsedMethod);
        var className = string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName;
        var methodName = string.IsNullOrWhiteSpace(test.MethodName) ? parsedMethod : test.MethodName;
        var testId = NUnitTestNameParser.ToIdeTestId(test.FullName, className, methodName, test.Name);
        TestingSourceLocation? location = null;
        if (source is not null
            && source.TryGetSourceLocation(test, out var filePath, out var lineNumber)
            && !string.IsNullOrWhiteSpace(filePath))
        {
            location = new TestingSourceLocation(filePath!, lineNumber);
        }

        return new TestingDiscoveredTest(
            testId,
            test.Name,
            test.FullName,
            className,
            methodName,
            location);
    }
}

internal static class NUnitFilterXml
{
    public static TestFilter Create(string? xml) =>
        string.IsNullOrWhiteSpace(xml) ? TestFilter.Empty : TestFilter.FromXml(xml);
}

internal sealed class NUnitLocalExploration : IDisposable
{
    private readonly NUnitTestAssemblyRunner? _runner;
    private readonly NUnitDiscoveryAssemblyLoad? _load;

    private NUnitLocalExploration(
        NUnitTestAssemblyRunner? runner,
        IReadOnlyList<ITest> leaves,
        NUnitSourceLocationProvider? source,
        NUnitDiscoveryAssemblyLoad? load)
    {
        _runner = runner;
        _load = load;
        Leaves = leaves;
        Source = source;
    }

    public IReadOnlyList<ITest> Leaves { get; }

    public NUnitSourceLocationProvider? Source { get; }

    public static NUnitLocalExploration Load(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(assemblyPath))
            return new NUnitLocalExploration(null, [], null, null);

        var load = NUnitDiscoveryAssemblyLoad.Open(assemblyPath);
        try
        {
            var runner = new NUnitTestAssemblyRunner(new NUnitTolerantAssemblyBuilder());
            var workDirectory = Path.GetDirectoryName(assemblyPath) ?? AppContext.BaseDirectory;
            runner.Load(
                load.Assembly,
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    [FrameworkPackageSettings.WorkDirectory] = workDirectory,
                    [FrameworkPackageSettings.NumberOfTestWorkers] = 0,
                });

            var root = runner.ExploreTests(TestFilter.Empty);
            if (root.RunState == RunState.NotRunnable)
            {
                var reason = root.Properties.Get(PropertyNames.SkipReason)?.ToString()
                             ?? "NUnit could not explore the assembly.";
                throw new HostTestDiscoveryFailedException(reason);
            }

            var leaves = new List<ITest>();
            CollectLeaves(root, leaves);
            return new NUnitLocalExploration(
                runner,
                leaves,
                new NUnitSourceLocationProvider(assemblyPath),
                load);
        }
        catch
        {
            load.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _ = _runner;
        _load?.Dispose();
    }

    private static void CollectLeaves(ITest test, List<ITest> leaves)
    {
        if (!test.IsSuite)
        {
            leaves.Add(test);
            return;
        }

        foreach (var child in test.Tests)
            CollectLeaves(child, leaves);
    }
}
