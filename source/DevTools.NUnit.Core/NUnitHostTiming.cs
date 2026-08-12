namespace DevTools.NUnit.Core;

/// <summary>Shared host/runner timing defaults for NUnit bridge clients.</summary>
public static class NUnitHostTiming
{
    /// <summary>Default timeout for discover/run pipe requests (<c>--host-timeout</c>).</summary>
    public const int DefaultHostRequestTimeoutSeconds = 60;

    /// <summary>Default wait for host pipe after launch (<c>--host-launch-timeout</c>).</summary>
    public const int DefaultHostLaunchTimeoutSeconds = 180;

    /// <summary>Named-pipe connect timeout before hello/discover/run.</summary>
    public const int HostPipeConnectTimeoutSeconds = 30;

    /// <summary>Poll interval while waiting for a pipe response or disconnect.</summary>
    public const int HostRequestPollIntervalMilliseconds = 25;

    /// <summary>Extra slack added to adapter runner-process kill timeout.</summary>
    public const int RunnerProcessTimeoutSlackSeconds = 30;

    /// <summary>
    /// Adapter <c>WaitForExit</c> budget for the Runner process.
    /// Always includes launch timeout: <c>HostLaunch=false</c> still cold-starts
    /// when no existing host pipe is found.
    /// </summary>
    public static int ComputeAdapterRunnerProcessTimeoutSeconds(
        int hostLaunchTimeoutSeconds,
        int hostTimeoutSeconds) =>
        hostLaunchTimeoutSeconds + hostTimeoutSeconds + RunnerProcessTimeoutSlackSeconds;
}
