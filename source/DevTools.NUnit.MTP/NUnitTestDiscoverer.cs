using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Loading;
using NUnit;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using static DevTools.NUnit.Runtime.NUnitNameSyntax;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Authoritative local discovery via <see cref="NUnitTestAssemblyRunner"/>.
/// Test ids are MTP uids (`Class.Method` or `Class.Method("TestName")` for
/// renamed leaves). Host <c>&lt;test&gt;</c> still uses NUnit
/// <see cref="ITest.FullName"/>. No host launch.
/// </summary>
public sealed class NUnitTestDiscoverer : ITestDiscoverer
{
    public IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection)
    {
        using var session = NUnitLocalExploration.Load(assemblyPath);
        var all = session.Leaves.Select(test => ToDiscovered(test, session.Source)).ToList();
        if (selection.Kind == TestSelectionKind.All)
            return all;

        var testIds = selection.Kind == TestSelectionKind.TestIds ? CleanIds(selection.TestIds) : [];
        var names = selection.Kind == TestSelectionKind.Names ? CleanIds(selection.Names) : [];
        if (selection.Kind == TestSelectionKind.TestIds && testIds.Count == 0 || testIds.Count == 0 && names.Count == 0)
            return [];

        var selected = new List<TestDiscoveredTest>();
        if (testIds.Count > 0)
        {
            selected.AddRange(all.Where(test => testIds.Any(id =>
                string.Equals(id, test.TestId, StringComparison.Ordinal)
                || string.Equals(id, test.FullName, StringComparison.Ordinal)
                || test.TestId.StartsWith(id + ArgOpen, StringComparison.Ordinal)
                || NUnitCollapsedSelection.Matches(id, test.TestId, test.FullName, null))));
        }

        if (names.Count > 0)
        {
            var xml = NUnitSelectionXml.ToFilterXml(names);
            var filter = NUnitFilterFactory.Create(xml);
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

    private static TestDiscoveredTest ToDiscovered(ITest test, NUnitSourceLocationProvider? source)
    {
        NUnitTestNameParser.SplitIde(
            test.FullName,
            out var className,
            out var namespaceName,
            out var typeName,
            out var parsedMethod);
        var methodName = string.IsNullOrWhiteSpace(test.MethodName) ? parsedMethod : test.MethodName;
        var testId = NUnitTestNameParser.ToIdeTestId(test.FullName, className, methodName, test.Name);
        TestSourceLocation? location = null;
        if (source is not null
            && source.TryGetSourceLocation(test, out var filePath, out var lineNumber)
            && !string.IsNullOrWhiteSpace(filePath))
        {
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            location = new TestSourceLocation(filePath!, lineNumber);
        }

        return new TestDiscoveredTest(
            testId,
            NUnitTestNameParser.AppendDisplayArguments(test.Name, typeName),
            test.FullName,
            className,
            methodName,
            location,
            namespaceName,
            NUnitTestNameParser.ToSourceTypeSegment(typeName));
    }
}

internal sealed class NUnitLocalExploration : IDisposable
{
    private readonly NUnitTestAssemblyRunner? _runner;
    private readonly DiscoveryAssemblyLoad? _load;

    private NUnitLocalExploration(
        NUnitTestAssemblyRunner? runner,
        IReadOnlyList<ITest> leaves,
        NUnitSourceLocationProvider? source,
        DiscoveryAssemblyLoad? load)
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

        var load = DiscoveryAssemblyLoad.Open(assemblyPath);
        try
        {
            var runner = new NUnitTestAssemblyRunner(new NUnitAssemblyBuilder());
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
                throw new TestingDiscoveryFailedException(reason);
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
