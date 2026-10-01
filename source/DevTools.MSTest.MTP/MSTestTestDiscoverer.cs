using System.Reflection;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

namespace DevTools.MSTest.MTP;

[UsedImplicitly]
public sealed class MSTestTestDiscoverer : ITestDiscoverer
{
    /// <summary>
    /// A filtered discovery returns the selected tests plus the tests they <c>[DependsOn]</c>
    /// (transitively): MSTest only orders prerequisites that are part of the run, so the set
    /// the testhost publishes and sends to the host has to contain them.
    /// </summary>
    public IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Kind == TestSelectionKind.FrameworkFilter)
            return [];
        if (selection.Kind == TestSelectionKind.TestIds && selection.TestIds.Count == 0)
            return [];

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
            throw new TestingDiscoveryFailedException("MSTest test assembly not found: " + fullPath);

        var testAssembly = LoadTestAssembly(fullPath);
        RegisterWhenAnotherCopyIsLoaded(testAssembly);
        var listed = MSTestListing.List(testAssembly);
        var selected = Select(listed, selection);
        return selection.Kind == TestSelectionKind.All
            ? selected
            : WithPrerequisites(testAssembly, listed, selected);
    }

    private static Assembly LoadTestAssembly(string fullPath)
    {
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (loaded.IsDynamic || loaded.Location.Length == 0)
                continue;
            if (string.Equals(Path.GetFullPath(loaded.Location), fullPath, StringComparison.OrdinalIgnoreCase))
                return loaded;
        }

        return Assembly.LoadFrom(fullPath);
    }

    private static void RegisterWhenAnotherCopyIsLoaded(Assembly testAssembly)
    {
        var name = testAssembly.GetName().Name;
        if (string.IsNullOrEmpty(name))
            return;

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (loaded.IsDynamic || ReferenceEquals(loaded, testAssembly))
                continue;
            if (!string.Equals(loaded.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            ReflectionMetadataHook.Register(
                testAssembly,
                Type.EmptyTypes,
                new Dictionary<Type, MethodInfo[]>());
            return;
        }
    }

    private static List<TestDiscoveredTest> WithPrerequisites(
        Assembly testAssembly,
        IReadOnlyList<TestDiscoveredTest> listed,
        List<TestDiscoveredTest> selected)
    {
        if (selected.Count == 0)
            return selected;

        var byId = new Dictionary<string, TestDiscoveredTest>(StringComparer.Ordinal);
        foreach (var test in listed)
            byId.TryAdd(test.TestId, test);

        return MSTestExpansion.Expand(testAssembly, listed, selected.Select(test => test.TestId))
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();
    }

    private static List<TestDiscoveredTest> Select(
        IReadOnlyList<TestDiscoveredTest> listed,
        TestSelection selection)
    {
        var matcher = TestSelectionMatcher.For(selection);
        return listed.Where(test => matcher.Matches(test)).ToList();
    }
}
