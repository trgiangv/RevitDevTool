using Microsoft.Testing.Platform.Builder;

namespace DevTools.NUnit.Mtp;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        if (testApplicationBuilder is null)
            throw new ArgumentNullException(nameof(testApplicationBuilder));
        testApplicationBuilder.CommandLine.AddProvider(() => new DevToolsNUnitCommandLineProvider());
        testApplicationBuilder.RegisterTestFramework(
            _ => new DevToolsNUnitFrameworkCapabilities(),
            (capabilities, serviceProvider) => new DevToolsNUnitFramework(capabilities, serviceProvider));
    }
}
