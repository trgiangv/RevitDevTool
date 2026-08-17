namespace DevTools.NUnit.TestAdapter;

using DevTools.NUnit.Provider;
using DevTools.Testing.Transport;

public static class DevToolsNUnitConstants
{
    public const string ExecutorUri = "executor://DevTools.NUnit.V1/";
    public const string TestIdProperty = "DevTools.NUnit.TestId";
    public const string TestFullNameProperty = "DevTools.NUnit.FullName";

    public const string HostEnvironmentVariable = NUnitRunnerPaths.HostEnvironmentVariable;
    public const string HostVersionEnvironmentVariable = NUnitRunnerPaths.HostVersionEnvironmentVariable;
    public const string RunnerPathEnvironmentVariable = NUnitRunnerPaths.RunnerPathEnvironmentVariable;

    public const string SidecarRunSettingsFileName = "DevTools.NUnit.runsettings";

    public const string DefaultHost = "Revit";
    public const string DefaultHostVersion = "2025";
    public const int DefaultHostTimeoutSeconds = TestingHostTiming.DefaultHostRequestTimeoutSeconds;
    public const int DefaultHostLaunchTimeoutSeconds = TestingHostTiming.DefaultHostLaunchTimeoutSeconds;
}
