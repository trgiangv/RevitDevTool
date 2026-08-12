using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime;

internal static class NUnitTestIdentityMapper
{
    public static string MapTestId(ITest test, NUnitTestIdentityRegistry identityRegistry) =>
        identityRegistry.GetTestId(test);

    public static string? MapParentTestId(ITest test, NUnitTestIdentityRegistry identityRegistry) =>
        identityRegistry.GetParentTestId(test);
}
