using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace DevTools.TestAdapter;

/// <summary>
/// Microsoft.Testing.Platform entry hook. Framework-specific discovery is
/// delegated to <see cref="AdapterBootstrap"/> and
/// <see cref="DevTools.Testing.Abstractions.MTP.HostMTPRegistration"/>.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    static TestingPlatformBuilderHook() => AdapterBootstrap.Initialize();

    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        if (testApplicationBuilder is null)
            throw new ArgumentNullException(nameof(testApplicationBuilder));
        testApplicationBuilder.CommandLine.AddProvider(() => new HostCommandLineProvider());
        // Empty MTP capabilities. UID is ITest.FullName (NUnit3
        // UseFullyQualifiedNameAsTestNodeUid). Do not advertise VSTest-bridge
        // extras that would invite non-FQN filter rewrite.
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new HostTestFramework(serviceProvider));
    }
}
