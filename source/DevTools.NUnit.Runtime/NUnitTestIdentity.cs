using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime;

/// <summary>
/// Cross-process NUnit identity is <see cref="ITest.FullName"/> — the value
/// <c>&lt;test&gt;</c> matches. <see cref="ITest.Id"/> is per-load and is not used.
/// </summary>
internal static class NUnitTestIdentity
{
    public static string Id(ITest test) => test.FullName;

    public static string? ParentId(ITest test)
    {
        var parent = test.Parent;
        if (parent is null || parent.Parent is null)
            return null;

        return parent.FullName;
    }
}
