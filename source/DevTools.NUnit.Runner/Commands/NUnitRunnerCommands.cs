using ConsoleAppFramework;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Runner.Services;
using DevTools.Utilities.Hosting;

namespace DevTools.NUnit.Runner.Commands;

internal sealed class NUnitRunnerCommands
{
    private readonly HostSession _hosts = new(new HostLaunchService());

    /// <summary>
    /// Discover NUnit tests from PE metadata. Does not start or contact a host.
    /// </summary>
    /// <param name="assembly">Test assembly path.</param>
    /// <param name="host">Host app (Revit, AutoCAD, Civil3D, ...).</param>
    /// <param name="hostVersion">Autodesk year (2024, 2026, ...).</param>
    /// <param name="name">NUnit method Name values (JSON array or a single name).</param>
    /// <param name="test">NUnit FullName values (JSON array or a single name).</param>
    /// <param name="filter">Raw NUnit TestFilter XML. Do not mix with --name/--test.</param>
    /// <param name="hostLaunch">Ignored on discover. Required by shared CLI parsing with run.</param>
    /// <param name="hostTimeout">Ignored on discover.</param>
    /// <param name="hostLaunchTimeout">Ignored on discover.</param>
    [Command("discover")]
    public Task<int> Discover(
        [Argument] string assembly,
        string host,
        string hostVersion,
        string[]? name = null,
        string[]? test = null,
        string? filter = null,
        bool hostLaunch = false,
        int hostTimeout = NUnitHostTiming.DefaultHostRequestTimeoutSeconds,
        int hostLaunchTimeout = NUnitHostTiming.DefaultHostLaunchTimeoutSeconds,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            NUnitRunnerCli.DiscoverCommand,
            assembly,
            host,
            hostVersion,
            name,
            test,
            filter,
            hostLaunch,
            hostTimeout,
            hostLaunchTimeout,
            debug: false,
            debugParentPid: null,
            cancellationToken);

    /// <summary>
    /// Run NUnit tests inside the Autodesk host.
    /// </summary>
    /// <param name="assembly">Test assembly path.</param>
    /// <param name="host">Host app (Revit, AutoCAD, Civil3D, ...).</param>
    /// <param name="hostVersion">Autodesk year (2024, 2026, ...).</param>
    /// <param name="name">NUnit method Name values (JSON array or a single name).</param>
    /// <param name="test">NUnit FullName values (JSON array or a single name).</param>
    /// <param name="filter">Raw NUnit TestFilter XML. Do not mix with --name/--test.</param>
    /// <param name="hostLaunch">Always launch a new host (skip reuse).</param>
    /// <param name="hostTimeout">Pipe request timeout in seconds.</param>
    /// <param name="hostLaunchTimeout">Wait for host pipe after launch, in seconds.</param>
    /// <param name="debug">Attach Visual Studio to the host (GetActiveObject when --debug-parent-pid is omitted).</param>
    /// <param name="debugParentPid">MTP/testhost PID. Presence implies --debug and selects that Visual Studio instance.</param>
    [Command("run")]
    public Task<int> Run(
        [Argument] string assembly,
        string host,
        string hostVersion,
        string[]? name = null,
        string[]? test = null,
        string? filter = null,
        bool hostLaunch = false,
        int hostTimeout = NUnitHostTiming.DefaultHostRequestTimeoutSeconds,
        int hostLaunchTimeout = NUnitHostTiming.DefaultHostLaunchTimeoutSeconds,
        bool debug = false,
        int? debugParentPid = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            NUnitRunnerCli.RunCommand,
            assembly,
            host,
            hostVersion,
            name,
            test,
            filter,
            hostLaunch,
            hostTimeout,
            hostLaunchTimeout,
            debug,
            debugParentPid,
            cancellationToken);

    private async Task<int> ExecuteAsync(
        string command,
        string assembly,
        string host,
        string hostVersion,
        string[]? name,
        string[]? test,
        string? filter,
        bool hostLaunch,
        int hostTimeout,
        int hostLaunchTimeout,
        bool debug,
        int? debugParentPid,
        CancellationToken cancellationToken)
    {
        if (!RunnerCommandLine.TryCreate(
                command,
                assembly,
                host,
                hostVersion,
                name,
                test,
                filter,
                hostLaunch,
                hostTimeout,
                hostLaunchTimeout,
                debug,
                debugParentPid,
                out var options,
                out var error))
        {
            await Console.Error.WriteLineAsync(error ?? "Invalid command line.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        return command == NUnitRunnerCli.RunCommand
            ? await RunCommand.ExecuteAsync(options!, _hosts, cancellationToken).ConfigureAwait(false)
            : await DiscoverCommand.ExecuteAsync(options!, cancellationToken).ConfigureAwait(false);
    }
}
