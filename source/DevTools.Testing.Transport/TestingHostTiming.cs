using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

/// <summary>
/// Adapter-side budgets around the TestRunner child process.
/// Host-facing timeouts are MSBuild properties on the consumer test project
/// (<c>HostTimeout</c>, <c>HostLaunchTimeout</c>) and arrive here as
/// <see cref="TestingHostOptions"/>. The constants below are local I/O slack
/// and are not csproj options.
/// </summary>
public static class TestingHostTiming
{
    /// <summary>
    /// Extra seconds on adapter <c>WaitForExit</c> after
    /// <c>HostLaunchTimeout + HostTimeout</c>. The TestRunner may still be
    /// flushing JSON after the in-host pipe request has already timed out.
    /// </summary>
    public const int RunnerProcessTimeoutSlackSeconds = 30;

    /// <summary>
    /// Drain redirected stdout/stderr after killing a timed-out TestRunner.
    /// </summary>
    public const int TimedOutProcessOutputDrainMilliseconds = 5_000;

    /// <summary>
    /// Finish reading redirected streams after TestRunner has already exited.
    /// </summary>
    public const int ExitedProcessOutputDrainMilliseconds = 30_000;

    /// <summary>
    /// Adapter <c>WaitForExit</c> budget for the TestRunner process.
    /// Always includes launch timeout: <c>HostLaunch=true</c> starts a host,
    /// and <c>HostLaunch=false</c> still cold-starts when no matching pipe exists.
    /// </summary>
    public static int ComputeAdapterRunnerProcessTimeoutSeconds(
        int hostLaunchTimeoutSeconds,
        int hostTimeoutSeconds) =>
        hostLaunchTimeoutSeconds + hostTimeoutSeconds + RunnerProcessTimeoutSlackSeconds;
}
