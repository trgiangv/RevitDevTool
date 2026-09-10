namespace DevTools.Testing.Abstractions.Config;

/// <summary>
/// Shared <c>testconfig.json</c> contract written by RevitDevTool.TestAdapter
/// and read by the adapter control plane.
/// </summary>
public static class TestConfig
{
    public const string FileName = "testconfig.json";
    public const string SectionName = "devtools";

    public static class Keys
    {
        public const string HostName = "hostName";
        public const string HostVersion = "hostVersion";
        public const string ForceLaunch = "forceLaunch";
        public const string PerTestTimeoutSeconds = "perTestTimeoutSeconds";
        public const string LaunchTimeoutSeconds = "launchTimeoutSeconds";
        public const string RunnerPath = "runnerPath";
        public const string FrameworkId = "frameworkId";

        public static string Configuration(string name) => SectionName + ":" + name;
    }
}
