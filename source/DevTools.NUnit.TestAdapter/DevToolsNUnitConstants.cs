namespace DevTools.NUnit.TestAdapter;

using DevTools.NUnit.Core;

public static class DevToolsNUnitConstants
{
    public const string ExecutorUri = "executor://DevTools.NUnit.V1/";
    public const string TestIdProperty = "DevTools.NUnit.TestId";
    public const string TestFullNameProperty = "DevTools.NUnit.FullName";

    public const string HostEnvironmentVariable = "DEVTOOLS_NUNIT_HOST";
    public const string HostVersionEnvironmentVariable = "DEVTOOLS_NUNIT_HOST_VERSION";
    public const string RunnerPathEnvironmentVariable = "DEVTOOLS_NUNIT_RUNNER_PATH";

    public const string SidecarRunSettingsFileName = "DevTools.NUnit.runsettings";

    public const string DefaultHost = "Revit";
    public const string DefaultHostVersion = "2025";
    public const int DefaultHostTimeoutSeconds = NUnitHostTiming.DefaultHostRequestTimeoutSeconds;
    public const int DefaultHostLaunchTimeoutSeconds = NUnitHostTiming.DefaultHostLaunchTimeoutSeconds;
}
