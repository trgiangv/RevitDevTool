using System.Text.Json;
using ConsoleAppFramework;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.TestRunner.Core.Services;

namespace DevTools.TestRunner;

public sealed class RunnerCommands(
    IExecutionCoordinator execution,
    IDebuggerAttach debugger,
    IMachineRunInput machineRunInput)
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
    /// <param name="debug">Attach the parent IDE to the Autodesk host (Visual Studio any-instance when --debug-parent-pid is omitted).</param>
    /// <param name="debugParentPid">MTP/testhost PID. Presence implies --debug and selects the Visual Studio instance debugging that process.</param>
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

        var invocation = new TestingRunInvocation(
            TestingProtocol.CurrentVersion,
            new TestingHostOptions(
                options.Context.HostName,
                options.Context.HostVersion,
                options.Context.ForceLaunch,
                options.Context.PerTestTimeoutSeconds,
                options.Context.LaunchTimeoutSeconds,
                RunnerPath: null,
                DebugParentPid: options.Context.DebugParentPid),
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                options.FrameworkId,
                new TestingAssemblyReference(options.AssemblyPath),
                options.Selection));

        return await ExecuteInvocation(invocation, options.Context, machine: false, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Machine Adapter → Runner entry. Stdin is one <see cref="TestingRunInvocation"/>
    /// JSON document; <see cref="TestingRunRequest.RunId"/> is preserved to the host.
    /// </summary>
    [Command("machine-run")]
    public async Task<int> MachineRun(CancellationToken cancellationToken = default)
    {
        var json = await ReadMachineRunJsonAsync(cancellationToken).ConfigureAwait(false);

        TestingRunInvocation? invocation;
        try
        {
            invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunInvocation);
        }
        catch (JsonException exception)
        {
            await Console.Error.WriteLineAsync($"Invalid machine-run JSON: {exception.Message}").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (invocation is null)
        {
            await Console.Error.WriteLineAsync("machine-run stdin was empty.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!TestingProtocol.IsCompatible(invocation.ProtocolVersion)
            || !TestingProtocol.IsCompatible(invocation.Run.ProtocolVersion))
        {
            var version = TestingProtocol.IsCompatible(invocation.ProtocolVersion)
                ? invocation.Run.ProtocolVersion
                : invocation.ProtocolVersion;
            await Console.Error.WriteLineAsync(TestingProtocol.CreateUnsupportedMessage(version))
                .ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!File.Exists(invocation.Run.Assembly.Path))
        {
            await Console.Error.WriteLineAsync($"Assembly not found: {invocation.Run.Assembly.Path}")
                .ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!RunnerCommandContext.TryCreate(
                invocation.Run.Assembly.Path,
                invocation.Host.HostName,
                invocation.Host.HostVersion,
                invocation.Host.ForceLaunch,
                invocation.Host.PerTestTimeoutSeconds,
                invocation.Host.LaunchTimeoutSeconds,
                debug: invocation.Host.DebugParentPid is > 0,
                invocation.Host.DebugParentPid,
                invocation.Run.FrameworkId,
                invocation.Host.EffectiveRequestTimeoutSeconds,
                out var context,
                out var error))
        {
            await Console.Error.WriteLineAsync(error ?? "Invalid machine-run invocation.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        return await ExecuteInvocation(invocation, context!, machine: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<string> ReadMachineRunJsonAsync(CancellationToken cancellationToken) =>
        machineRunInput.ReadToEndAsync(cancellationToken);

    private async Task<int> ExecuteInvocation(
        TestingRunInvocation invocation,
        RunnerCommandContext context,
        bool machine,
        CancellationToken cancellationToken)
    {
        using var cancelSignal = TestingCancelSignal.Create(invocation.Run.RunId);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() =>
        {
            try
            {
                if (WaitHandle.WaitAny([cancelSignal, linked.Token.WaitHandle]) == 0)
                    linked.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        });

        var progress = new Progress<TestingEvent>(testingEvent =>
        {
            if (machine)
            {
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        new TestingRunnerStreamMessage(Event: testingEvent),
                        TestingJsonContext.Default.TestingRunnerStreamMessage));
                return;
            }

            if (testingEvent.Case is { } result)
                Console.Error.WriteLine($"[progress] {result.DisplayName} -> {result.Outcome}");
        });

        var result = await execution.ExecuteAsync(
                context,
                debugger,
                async (pipe, requestCancellationToken) =>
                {
                    await using var client = await TestPipeClient.ConnectAsync(
                            pipe.PipeName,
                            TimeSpan.FromSeconds(TestingHostTiming.HostPipeConnectTimeoutSeconds),
                            requestCancellationToken)
                        .ConfigureAwait(false);
                    var hello = await client.HelloAsync(invocation.Run.FrameworkId, requestCancellationToken)
                        .ConfigureAwait(false);
                    EnsureHelloMatches(hello, invocation);
                    return await client.RunAsync(invocation.Run, progress, requestCancellationToken)
                        .ConfigureAwait(false);
                },
                linked.Token)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            await Console.Error.WriteLineAsync(result.Error ?? "Host execution failed.").ConfigureAwait(false);
            return result.Failure switch
            {
                ExecutionFailure.InvalidHost => RunnerExitCode.CliError,
                ExecutionFailure.TimedOut => RunnerExitCode.RequestTimeout,
                _ => RunnerExitCode.NoHost,
            };
        }

        if (machine)
        {
            Console.WriteLine(
                JsonSerializer.Serialize(
                    new TestingRunnerStreamMessage(Response: result.Value),
                    TestingJsonContext.Default.TestingRunnerStreamMessage));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(result.Value, TestingJsonContext.Default.TestingRunResponse));
        }

        return HasTestingFailure(result.Value!) ? RunnerExitCode.TestFailure : RunnerExitCode.Ok;
    }

    private static void EnsureHelloMatches(TestingHelloResponse hello, TestingRunInvocation invocation)
    {
        if (!string.Equals(hello.FrameworkId, invocation.Run.FrameworkId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Host framework '{hello.FrameworkId}' does not match '{invocation.Run.FrameworkId}'.");
        }

        if (!string.Equals(hello.Host, invocation.Host.HostName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Host '{hello.Host}' does not match requested '{invocation.Host.HostName}'.");
        }

        if (!string.Equals(hello.HostVersion, invocation.Host.HostVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Host version '{hello.HostVersion}' does not match requested '{invocation.Host.HostVersion}'.");
        }

        if (hello.IsBusy)
            throw new InvalidOperationException("Host testing session is busy.");
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
