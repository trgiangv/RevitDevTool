namespace DevTools.NUnit.Core.Contracts;

public static class NUnitProtocol
{
    public const int CurrentVersion = 1;

    public const string Hello = "nunit/hello";
    public const string Discover = "nunit/discover";
    public const string Run = "nunit/run";
    public const string Cancel = "nunit/cancel";
    public const string Progress = "nunit/progress";
    public const string DebugReady = "nunit/debug-ready";
}
