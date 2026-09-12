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
/// renamed leaves). NUnit 4 numeric suffixes on args (`12.3d`) are stripped so
/// the uid matches in-host <c>ITest.FullName</c>. Host <c>&lt;test&gt;</c>
/// still uses NUnit <see cref="ITest.FullName"/>. No host launch.
/// </summary>
public sealed class NUnitTestDiscoverer : ITestDiscoverer
{
    public IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection)
    {
        using var session = NUnitLocalExploration.Load(assemblyPath);
        var all = MapAll(session);
        if (selection.Kind == TestSelectionKind.All)
            return all;

        var testIds = selection.Kind == TestSelectionKind.TestIds ? CleanIds(selection.TestIds) : [];
        var names = selection.Kind == TestSelectionKind.Names ? CleanIds(selection.Names) : [];
        if (testIds.Count == 0 && names.Count == 0)
            return [];

        var selected = new List<TestDiscoveredTest>();
        if (testIds.Count > 0)
            selected.AddRange(NUnitIdentityIndex.Build(all).Select(testIds));

        if (names.Count > 0)
            selected.AddRange(SelectByNames(session, all, names));

        return Deduplicate(selected);
    }

    private static List<TestDiscoveredTest> MapAll(NUnitLocalExploration session) =>
        session.Leaves.Select(test => ToDiscovered(test, session.Source)).ToList();

    private static IEnumerable<TestDiscoveredTest> SelectByNames(
        NUnitLocalExploration session,
        IReadOnlyList<TestDiscoveredTest> tests,
        IReadOnlyList<string> names)
    {
        var xml = NUnitSelectionXml.ToFilterXml(names);
        var filter = NUnitFilterFactory.Create(xml);
        var nameHits = new HashSet<string>(
            session.Leaves.Where(filter.Pass).Select(test => test.FullName),
            StringComparer.Ordinal);
        return tests.Where(test =>
            !string.IsNullOrWhiteSpace(test.FullName) && nameHits.Contains(test.FullName!));
    }

    private static List<TestDiscoveredTest> Deduplicate(IEnumerable<TestDiscoveredTest> tests) =>
        tests
            .GroupBy(test => test.TestId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

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
        var nunitName = test.FullName;
        var testId = StripNumericTypeSuffixes(
            NUnitTestNameParser.ToIdeTestId(nunitName, className, methodName, test.Name));
        var fullName = StripNumericTypeSuffixes(nunitName);
        TestSourceLocation? location = null;
        if (source is not null
            && source.TryGetSourceLocation(test, out var filePath, out var lineNumber)
            && !string.IsNullOrWhiteSpace(filePath))
        {
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            location = new TestSourceLocation(filePath!, lineNumber);
        }

        var methodInfo = test.Method?.MethodInfo;
        ReadMethodSignature(methodInfo, out var parameterTypes, out var returnType);
        return new TestDiscoveredTest(
            testId,
            StripNumericTypeSuffixes(
                NUnitTestNameParser.AppendDisplayArguments(test.Name, typeName)),
            fullName,
            className,
            methodName,
            location,
            namespaceName,
            NUnitTestNameParser.ToSourceTypeSegment(typeName),
            ParameterTypeFullNames: parameterTypes,
            ReturnTypeFullName: returnType);
    }

    /// <summary>
    /// <c>GetParameters</c> / <c>ReturnType</c> load parameter assemblies.
    /// Compile-only Autodesk refs are often a NuGet <c>ref/</c> path that the
    /// testhost cannot load — that must not abort ExploreTests mapping.
    /// </summary>
    private static void ReadMethodSignature(
        System.Reflection.MethodInfo? methodInfo,
        out IReadOnlyList<string>? parameterTypes,
        out string? returnType)
    {
        parameterTypes = null;
        returnType = null;
        if (methodInfo is null)
            return;

        try
        {
            parameterTypes = methodInfo.GetParameters()
                .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
                .ToArray();
            returnType = methodInfo.ReturnType.FullName;
        }
        catch (Exception ex) when (
            ex is FileNotFoundException
            or FileLoadException
            or TypeLoadException
            or BadImageFormatException)
        {
        }
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
