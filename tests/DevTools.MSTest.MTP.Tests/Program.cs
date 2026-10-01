using System.Reflection;
using DevTools.MSTest.MTP;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.MSTest.MTP.Tests;

public static class Program
{
    public static int Main()
    {
        try
        {
            var failed = 0;
            failed += Check("discover-does-not-execute", DiscoverDoesNotExecuteTestBodies);
            failed += Check("run-by-id-selects-depends-on-prerequisites", RunByIdSelectsPrerequisites);
            failed += Check("type-level-depends-on-selects-every-target-test", TypeLevelDependsOnSelectsTargetTests);
            failed += Check("select-all-and-empty-are-unchanged", AllAndEmptyAreUnchanged);
            failed += Check("data-rows-are-distinct-uids", DataRowsAreDistinctUids);
            return failed;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            return 1;
        }
    }

    private static int Check(string name, Action check)
    {
        try
        {
            check();
            Console.WriteLine("PASS " + name);
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL " + name);
            Console.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>The methods a filtered discovery returns: what the host will run.</summary>
    private static List<string> Discovered(params string[] methodNames)
    {
        var path = FindProbe();
        var discoverer = new MSTestTestDiscoverer();
        var all = discoverer.Discover(path, TestSelection.All);
        var ids = methodNames.Select(method => all.Single(test => test.MethodName == method).TestId).ToList();
        return discoverer.Discover(path, TestSelection.FromTestIds(ids)).Select(test => test.MethodName!).ToList();
    }

    private static void RunByIdSelectsPrerequisites()
    {
        AssertSet(Discovered("Three"), "Three", "Two", "One");
    }

    private static void TypeLevelDependsOnSelectsTargetTests()
    {
        AssertSet(Discovered("Audit"), "Audit", "One", "Two", "Three", "Unrelated");
    }

    private static void AllAndEmptyAreUnchanged()
    {
        var path = FindProbe();
        var discoverer = new MSTestTestDiscoverer();
        var all = discoverer.Discover(path, TestSelection.All);
        if (!all.Any(test => test.MethodName == "Unrelated") || !all.Any(test => test.MethodName == "Three"))
            throw new InvalidOperationException("All selection lost tests.");

        var empty = discoverer.Discover(path, TestSelection.FromTestIds([]));
        if (empty.Count != 0)
            throw new InvalidOperationException("Empty selection returned " + empty.Count + " tests.");

        AssertSet(Discovered("Unrelated"), "Unrelated");
    }

    private static void DataRowsAreDistinctUids()
    {
        var path = FindProbe();
        var discoverer = new MSTestTestDiscoverer();
        var all = discoverer.Discover(path, TestSelection.All);
        AssertRows(all, "Basis", 3, "Unit_X", "Unit_Y", "Unit_Z");
        AssertRows(all, "Magnitude", 3);
        AssertRows(all, "FromClass", 2);

        var one = all.Single(test => test.DisplayName == "Unit_X");
        var selected = discoverer.Discover(path, TestSelection.FromTestIds([one.TestId]));
        if (selected.Count != 1 || selected[0].TestId != one.TestId)
        {
            throw new InvalidOperationException(
                "Selecting one data row returned "
                + string.Join(", ", selected.Select(test => test.DisplayName)));
        }
    }

    private static void AssertRows(
        IReadOnlyList<TestDiscoveredTest> all,
        string methodName,
        int count,
        params string[] displayNames)
    {
        var rows = all.Where(test => test.MethodName == methodName).ToList();
        if (rows.Count != count
            || rows.Select(test => test.TestId).Distinct(StringComparer.Ordinal).Count() != count)
        {
            throw new InvalidOperationException(
                methodName + " rows=" + rows.Count
                + " ids=" + string.Join(", ", rows.Select(test => test.TestId + ":" + test.DisplayName)));
        }

        foreach (var name in displayNames)
        {
            if (!rows.Any(test => test.DisplayName == name))
                throw new InvalidOperationException(methodName + " missing display name " + name);
        }
    }
    private static void AssertSet(List<string> actual, params string[] expected)
    {
        if (actual.Count != expected.Length || expected.Any(name => !actual.Contains(name)))
        {
            throw new InvalidOperationException(
                "Expected {" + string.Join(", ", expected) + "} but got {" + string.Join(", ", actual) + "}");
        }
    }

    private static void DiscoverDoesNotExecuteTestBodies()
    {
        var assemblyPath = FindProbe();
        var marker = Path.Combine(Path.GetDirectoryName(assemblyPath)!, "mstest-body-executed.txt");
        if (File.Exists(marker))
            File.Delete(marker);

        var discovered = new MSTestTestDiscoverer().Discover(assemblyPath, TestSelection.All);
        var probe = LoadProbe(assemblyPath);
        var executions = probe.GetField("Executions", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (!Equals(executions, 0))
            throw new InvalidOperationException("list-tests executed a test body. Executions=" + executions);
        if (File.Exists(marker))
            throw new InvalidOperationException("list-tests wrote the body side-effect file.");

        var listed = discovered.SingleOrDefault(test =>
            test.FullName?.EndsWith(".CounterTests.Increments", StringComparison.Ordinal) == true);
        if (listed is null)
        {
            throw new InvalidOperationException(
                "Discovered: " + string.Join(", ", discovered.Select(test => test.FullName)));
        }

        if (!Guid.TryParse(listed.TestId, out _))
            throw new InvalidOperationException("TestId is not an MTP uid: " + listed.TestId);
        if (string.Equals(listed.TestId, listed.FullName, StringComparison.Ordinal))
            throw new InvalidOperationException("TestId reused the method name " + listed.TestId);
        if (!string.Equals(listed.DisplayName, "Increments", StringComparison.Ordinal))
            throw new InvalidOperationException("DisplayName " + listed.DisplayName);
    }

    private static string FindProbe()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "DevTools.MSTest.MTP.Tests.csproj")))
            root = root.Parent;
        if (root is null)
            throw new InvalidOperationException("Could not locate DevTools.MSTest.MTP.Tests.csproj.");

        var bin = Path.Combine(root.FullName, "Fixtures", "NoExecute", "bin");
        var matches = Directory.Exists(bin)
            ? Directory.GetFiles(bin, "DevTools.MSTest.NoExecuteProbe.dll", SearchOption.AllDirectories)
            : [];
        if (matches.Length == 0)
            throw new FileNotFoundException("No-execute probe was not built.", bin);

        return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static Type LoadProbe(string assemblyPath)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            !assembly.IsDynamic
            && assembly.Location.Length > 0
            && string.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase));
        loaded ??= Assembly.LoadFrom(assemblyPath);
        return loaded.GetType("DevTools.TestAdapter.Tests.Fixtures.Probe", throwOnError: true)
            ?? throw new InvalidOperationException("Probe type was not in the fixture assembly.");
    }
}
