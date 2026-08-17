namespace DevTools.NUnit.Transport.Contracts;

public static class NUnitProtocol
{
    public const int CurrentVersion = 2;

    public const string Hello = "nunit/hello";
    public const string Discover = "nunit/discover";
    public const string Run = "nunit/run";
    public const string Cancel = "nunit/cancel";
    public const string Progress = "nunit/progress";
}
