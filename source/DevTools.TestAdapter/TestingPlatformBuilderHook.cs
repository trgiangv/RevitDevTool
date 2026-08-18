using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace DevTools.TestAdapter;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    // The package keeps its provider implementation as a runtime-only asset.
    // Register before the platform host asks this public hook to create that provider.
    static TestingPlatformBuilderHook() => RuntimeAssemblyResolver.EnsureRegistered();

    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        if (testApplicationBuilder is null)
            throw new ArgumentNullException(nameof(testApplicationBuilder));
        testApplicationBuilder.CommandLine.AddProvider(() => new HostCommandLineProvider());
        // Empty capabilities: discover/run still work.
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new HostTestFramework(serviceProvider));
    }
}
