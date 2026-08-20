using System.Reflection;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace DevTools.TestAdapter;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// NUnit provider loaded from the test output directory. Not ILRepacked
    /// into this adapter. These three values are the plug-in contract with
    /// <c>DevTools.NUnit.MTP</c>; rename them together or discovery fails.
    /// </summary>
    public const string NUnitMTPAssemblyFileName = "DevTools.NUnit.MTP.dll";

    /// <summary>Public type in <see cref="NUnitMTPAssemblyFileName"/>.</summary>
    public const string NUnitMTPEntryTypeName = "DevTools.NUnit.MTP.NUnitMTP";

    /// <summary>Public static entry that assigns <see cref="HostTestDiscovery.Provider"/>.</summary>
    public const string NUnitMTPRegisterMethodName = "Register";

    // The package keeps its provider implementation as a runtime-only asset.
    // Register before the platform host asks this public hook to create that provider.
    static TestingPlatformBuilderHook()
    {
        RuntimeAssemblyResolver.EnsureRegistered();
        TryRegisterNUnitMTP();
    }

    internal static void TryRegisterNUnitMTP()
    {
        var path = Path.Combine(AppContext.BaseDirectory, NUnitMTPAssemblyFileName);
        if (!File.Exists(path))
            return;

        var assembly = RuntimeAssemblyResolver.LoadUnlocked(path);
        var type = assembly.GetType(NUnitMTPEntryTypeName, throwOnError: false);
        type?.GetMethod(NUnitMTPRegisterMethodName, BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }

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
