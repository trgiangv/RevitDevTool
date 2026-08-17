namespace DevTools.TestRunner.Core.Parsing;

public static class RunnerExitCode
{
    public const int Ok = 0;
    public const int TestFailure = 1;
    public const int CliError = 2;
    public const int NoHost = 3;
    public const int HostTimeout = 4;
}
