using ConsoleAppFramework;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Services;
using DevTools.NUnit.Runner.Parsing;
using DevTools.TestRunner.Core.Composition;
using DevTools.TestRunner.Core.Parsing;

namespace DevTools.NUnit.Runner.Commands;

public sealed class NUnitRunnerCommands(
    RunnerModuleRegistry modules,
    IHostExecutionCoordinator execution,
    IVisualStudioAttach debugger)
{
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
    /// <param name="framework">Host test framework id. Defaults to NUnit.</param>
    [Command("discover")]
    public Task<int> Discover(
        [Argument] string assembly,
        string host,
        string hostVersion,
        string[]? name = null,
        string[]? test = null,
        string? filter = null,
        bool hostLaunch = false,
        int hostTimeout = TestingHostTiming.DefaultHostRequestTimeoutSeconds,
        int hostLaunchTimeout = TestingHostTiming.DefaultHostLaunchTimeoutSeconds,
        string framework = "",
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
            framework,
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
    /// <param name="framework">Host test framework id. Defaults to NUnit.</param>
    [Command("run")]
    public Task<int> Run(
        [Argument] string assembly,
        string host,
        string hostVersion,
        string[]? name = null,
        string[]? test = null,
        string? filter = null,
        bool hostLaunch = false,
        int hostTimeout = TestingHostTiming.DefaultHostRequestTimeoutSeconds,
        int hostLaunchTimeout = TestingHostTiming.DefaultHostLaunchTimeoutSeconds,
        bool debug = false,
        int? debugParentPid = null,
        string framework = "",
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
            framework,
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
        string framework,
        CancellationToken cancellationToken)
    {
        if (!RunnerCommandContext.TryCreate(
                modules,
                command,
                assembly,
                host,
                hostVersion,
                hostLaunch,
                hostTimeout,
                hostLaunchTimeout,
                debug,
                debugParentPid,
                framework,
                out var context,
                out var error)
            || !RunnerCommandLine.TryCreate(
                context!,
                name,
                test,
                filter,
                out var options,
                out error))
        {
            await Console.Error.WriteLineAsync(error ?? "Invalid command line.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        return command == NUnitRunnerCli.RunCommand
            ? await RunCommand.ExecuteAsync(options!, execution, debugger, cancellationToken).ConfigureAwait(false)
            : await DiscoverCommand.ExecuteAsync(options!, cancellationToken).ConfigureAwait(false);
    }
}
