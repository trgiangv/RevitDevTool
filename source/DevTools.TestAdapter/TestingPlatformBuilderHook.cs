using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace DevTools.TestAdapter;

/// <summary>
/// Microsoft.Testing.Platform entry hook. Registers the adapter
/// <see cref="TestFramework"/>. Framework-specific discovery is assigned by the
/// selected sibling hook via TestingDiscovery.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(testApplicationBuilder);
        _ = arguments;
        testApplicationBuilder.CommandLine.AddProvider(() => new TestCommandLineProvider());
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new TestFramework(serviceProvider));
    }
}
