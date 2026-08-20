using DevTools.Testing.Abstractions;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Plug-in entry loaded by <c>TestingPlatformBuilderHook</c> via
/// <c>NUnitMTPAssemblyFileName</c> / <c>NUnitMTPEntryTypeName</c> /
/// <c>NUnitMTPRegisterMethodName</c>. Keep those constants in lockstep with
/// this type and method name.
/// </summary>
public static class NUnitMTP
{
    public static void Register() =>
        HostTestDiscovery.Provider = new NUnitHostTestDiscoverer();
}
