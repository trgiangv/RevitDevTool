using System.Runtime.CompilerServices;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime;

internal sealed class NUnitTestIdentityRegistry
{
    private readonly Dictionary<ITest, string> _testIds = new(ReferenceEqualityComparer.Instance);

    public static NUnitTestIdentityRegistry Build(ITest root)
    {
        var registry = new NUnitTestIdentityRegistry();
        registry.AssignRecursive(root, parentId: null);
        return registry;
    }

    public string GetTestId(ITest test) =>
        _testIds.TryGetValue(test, out var testId) ? testId : FallbackTestId(test);

    public string? GetParentTestId(ITest test)
    {
        var parent = test.Parent;
        if (parent is null || parent.Parent is null)
            return null;

        return GetTestId(parent);
    }

    public bool Contains(ITest test) => _testIds.ContainsKey(test);

    private void AssignRecursive(ITest test, string? parentId)
    {
        var testId = parentId is null
            ? test.FullName
            : FormChildId(parentId, test);

        _testIds[test] = testId;

        var children = test.Tests;
        for (var index = 0; index < children.Count; index++)
            AssignRecursive(children[index], testId);
    }

    private static string FormChildId(string parentId, ITest test)
    {
        var siblingIndex = GetSiblingIndex(test);
        return string.Concat(parentId, "/", test.Name, "#", siblingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static int GetSiblingIndex(ITest test)
    {
        var parent = test.Parent;
        if (parent is null)
            return 0;

        var children = parent.Tests;
        for (var index = 0; index < children.Count; index++)
        {
            if (ReferenceEquals(children[index], test))
                return index;
        }

        return 0;
    }

    private static string FallbackTestId(ITest test)
    {
        var parent = test.Parent;
        if (parent is null || parent.Parent is null)
            return test.FullName;

        return FormChildId(FallbackTestId(parent), test);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<ITest>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public bool Equals(ITest? x, ITest? y) => ReferenceEquals(x, y);

        public int GetHashCode(ITest obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
