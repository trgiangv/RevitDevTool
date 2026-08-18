using System.Text.Json;
using ConsoleAppFramework;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.TestRunner.Core.Services;

namespace DevTools.TestRunner;

public sealed class RunnerCommands(
    IHostExecutionCoordinator execution,
    IVisualStudioAttach debugger)
{
    /// <summary>
    /// Run tests inside the Autodesk host through <c>testing/run</c>.
    /// Framework id is an opaque token from the test project (MTP <c>devtools</c> section).
    /// </summary>
    /// <param name="assembly">Test assembly path.</param>
    /// <param name="host">Host app (Revit, AutoCAD, Civil3D, ...).</param>
    /// <param name="hostVersion">Autodesk year (2024, 2026, ...).</param>
    /// <param name="name">Test method names (JSON array or a single name).</param>
    /// <param name="test">Test ids / full names (JSON array or a single name).</param>
    /// <param name="filter">Opaque in-host filter payload. Do not mix with --name/--test.</param>
    /// <param name="forceLaunch">Always launch a new host (skip reuse).</param>
    /// <param name="perTestTimeout">Per-test budget in seconds. When launched from the adapter this is already scaled by the run's test count.</param>
    /// <param name="launchTimeout">Wait for host pipe after launch, in seconds.</param>
    /// <param name="debug">Attach Visual Studio to the host (GetActiveObject when --debug-parent-pid is omitted).</param>
    /// <param name="debugParentPid">MTP/testhost PID. Presence implies --debug and selects that Visual Studio instance.</param>
    /// <param name="framework">In-host engine id from the test project <c>devtools</c> section.</param>
    [Command("run")]
    public async Task<int> Run(
        [Argument] string assembly,
        string host,
        string hostVersion,
        string[]? name = null,
        string[]? test = null,
        string? filter = null,
        bool forceLaunch = false,
        int perTestTimeout = TestingHostTiming.DefaultPerTestTimeoutSeconds,
        int launchTimeout = TestingHostTiming.DefaultLaunchTimeoutSeconds,
        bool debug = false,
        int? debugParentPid = null,
        string framework = "",
        CancellationToken cancellationToken = default)
    {
        if (!RunnerCommandContext.TryCreate(
                TestingRunnerCli.RunCommand,
                assembly,
                host,
                hostVersion,
                forceLaunch,
                perTestTimeout,
                launchTimeout,
                debug,
                debugParentPid,
                framework,
                out var context,
                out var error)
            || !RunnerCommandLine.TryCreate(context!, name, test, filter, out var options, out error))
        {
            await Console.Error.WriteLineAsync(error ?? "Invalid command line.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!File.Exists(options!.AssemblyPath))
        {
            await Console.Error.WriteLineAsync($"Assembly not found: {options.AssemblyPath}").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        var progress = new Progress<TestingCaseResult>(result =>
        {
            Console.Error.WriteLine($"[progress] {result.DisplayName} -> {result.Outcome}");
        });

        var result = await execution.RunTestingAsync(
                options.Context,
                new TestingRunRequest(
                    TestingProtocol.CurrentVersion,
                    Guid.NewGuid(),
                    options.FrameworkId,
                    new TestingAssemblyReference(options.AssemblyPath, null, null),
                    options.Selection,
                    new Dictionary<string, string>()),
                progress,
                TimeSpan.FromSeconds(TestingHostTiming.HostPipeConnectTimeoutSeconds),
                debugger,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            await Console.Error.WriteLineAsync(result.Error ?? "Host execution failed.").ConfigureAwait(false);
            return result.Failure switch
            {
                HostExecutionFailure.InvalidHost => RunnerExitCode.CliError,
                HostExecutionFailure.TimedOut => RunnerExitCode.RequestTimeout,
                _ => RunnerExitCode.NoHost,
            };
        }

        Console.WriteLine(JsonSerializer.Serialize(result.Value, TestingJsonContext.Default.TestingRunResponse));
        return HasTestingFailure(result.Value!) ? RunnerExitCode.TestFailure : RunnerExitCode.Ok;
    }

    private static bool HasTestingFailure(TestingRunResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.DiagnosticCode))
            return true;

        if (response.CancellationState is TestingCancellationState.Completed
            or TestingCancellationState.Poisoned)
            return true;

        return response.Results.Any(result =>
            string.Equals(result.Outcome, TestingOutcomes.Failed, StringComparison.Ordinal)
            || string.Equals(result.Outcome, TestingOutcomes.Error, StringComparison.Ordinal)
            || string.Equals(result.Outcome, TestingOutcomes.Cancelled, StringComparison.Ordinal));
    }
}
