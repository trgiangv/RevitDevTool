using System.Reflection;
using DevTools.Testing.Abstractions;
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

    public const string TUnitMTPAssemblyFileName = "DevTools.TUnit.MTP.dll";
    public const string TUnitMTPEntryTypeName = "DevTools.TUnit.MTP.TUnitMTP";
    public const string TUnitMTPRegisterMethodName = "Register";

    /// <summary>
    /// Set when sibling load or <c>Register</c> fails. Must not throw from
    /// the static constructor: that aborts testhost before MTP can publish
    /// an error node (VS/C# Dev Kit: "Test discovery aborted: 0 Tests found").
    /// </summary>
    internal static string? RegistrationError { get; private set; }

    // The package keeps its provider implementation as a runtime-only asset.
    // Register before the platform host asks this public hook to create that provider.
    static TestingPlatformBuilderHook()
    {
        RuntimeAssemblyResolver.EnsureRegistered();
        if (!TryRegister(
                TUnitMTPAssemblyFileName,
                TUnitMTPEntryTypeName,
                TUnitMTPRegisterMethodName))
            TryRegisterNUnitMTP();
    }

    internal static void TryRegisterNUnitMTP()
    {
        RegistrationError = null;
        _ = TryRegister(NUnitMTPAssemblyFileName, NUnitMTPEntryTypeName, NUnitMTPRegisterMethodName);
    }

    private static bool TryRegister(string assemblyFileName, string entryTypeName, string registerMethodName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, assemblyFileName);
        if (!File.Exists(path))
            return false;

        try
        {
            var assembly = RuntimeAssemblyResolver.LoadUnlocked(path);
            var type = assembly.GetType(entryTypeName, throwOnError: false);
            type?.GetMethod(registerMethodName, BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
            if (HostTestDiscovery.Provider is null)
            {
                RegistrationError =
                    $"{entryTypeName}.{registerMethodName} did not assign HostTestDiscovery.Provider.";
            }
        }
        catch (Exception ex)
        {
            var failure = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            RegistrationError = failure.ToString();
        }

        return HostTestDiscovery.Provider is not null;
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
