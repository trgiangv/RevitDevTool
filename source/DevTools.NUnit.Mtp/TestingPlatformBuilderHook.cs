using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace DevTools.NUnit.Mtp;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        if (testApplicationBuilder is null)
            throw new ArgumentNullException(nameof(testApplicationBuilder));
        testApplicationBuilder.CommandLine.AddProvider(() => new DevToolsNUnitCommandLineProvider());
        // Empty capabilities: discover/run still work.
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new DevToolsNUnitFramework(serviceProvider));
    }
}
