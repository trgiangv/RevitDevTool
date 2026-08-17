using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public static class TestingRunnerCli
{
    public const string RunCommand = "run";
    public const string FrameworkOption = "--framework";
    public const string HostOption = "--host";
    public const string HostVersionOption = "--host-version";
    public const string HostLaunchOption = "--host-launch";
    public const string HostTimeoutOption = "--host-timeout";
    public const string HostLaunchTimeoutOption = "--host-launch-timeout";
    public const string DebugParentPidOption = "--debug-parent-pid";

    /// <summary>
    /// Builds TestRunner CLI args. <c>HostTimeout</c> / <c>HostLaunchTimeout</c>
    /// from the consumer csproj are forwarded as <c>--host-timeout</c> /
    /// <c>--host-launch-timeout</c> for the in-host pipe. The adapter
    /// <c>WaitForExit</c> budget is computed separately by
    /// <see cref="TestingHostTiming"/>.
    /// </summary>
    public static List<string> BuildRunArguments(
        TestingRunRequest request,
        TestingHostOptions hostOptions)
    {
        var args = new List<string>
        {
            RunCommand,
            FrameworkOption,
            request.FrameworkId,
            request.Assembly.Path,
            HostOption,
            hostOptions.Host,
            HostVersionOption,
            hostOptions.HostVersion,
            HostTimeoutOption,
            hostOptions.HostTimeoutSeconds.ToString(),
            HostLaunchTimeoutOption,
            hostOptions.HostLaunchTimeoutSeconds.ToString(),
        };

        if (hostOptions.HostLaunch)
            args.Add(HostLaunchOption);

        if (hostOptions.DebugParentPid is > 0)
        {
            args.Add(DebugParentPidOption);
            args.Add(hostOptions.DebugParentPid.Value.ToString());
        }

        return args;
    }
}
