using Microsoft.Testing.Platform.Builder;

namespace DevTools.MSTest.Runtime;

internal static class MSTestPlatformPolicy
{
    private static readonly Version ExpectedFrameworkVersion = new(4, 5, 1, 0);
    private static readonly Version ExpectedPlatformVersion = new(2, 5, 1, 0);

    public static void EnsurePinnedVersions()
    {
        var framework = typeof(TestClassAttribute).Assembly;
        var platform = typeof(TestApplication).Assembly;
        var frameworkVersion = framework.GetName().Version;
        var platformVersion = platform.GetName().Version;
        if (frameworkVersion == ExpectedFrameworkVersion
            && platformVersion == ExpectedPlatformVersion)
        {
            return;
        }

        throw new InvalidOperationException(
            "MSTest runtime requires loaded MSTest.TestFramework 4.5.1 and Microsoft.Testing.Platform 2.5.1. Found "
            + framework.GetName().Name + " " + frameworkVersion + " and "
            + platform.GetName().Name + " " + platformVersion + ".");
    }
}
