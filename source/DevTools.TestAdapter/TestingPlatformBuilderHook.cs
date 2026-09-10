using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace DevTools.TestAdapter;

/// <summary>
/// Microsoft.Testing.Platform entry hook. Registers the host-owned outer
/// test framework. Framework-specific discovery is assigned by the selected
/// sibling hook via HostTestDiscovery.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(testApplicationBuilder);
        _ = arguments;
        testApplicationBuilder.CommandLine.AddProvider(() => new HostCommandLineProvider());
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new HostTestFramework(serviceProvider));
    }
}
