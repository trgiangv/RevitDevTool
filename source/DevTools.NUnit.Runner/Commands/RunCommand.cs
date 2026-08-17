using System.Text.Json;
using DevTools.NUnit.Provider;
using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Results;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Runner.Services;
using DevTools.NUnit.Transport;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.TestRunner.Core.Services;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.Runner.Commands;

public static class RunCommand
{
    public static Task<int> ExecuteAsync(
        RunnerCommandLine options,
        IHostExecutionCoordinator execution,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(options, execution, VisualStudioAttach.Instance, cancellationToken);

    internal static async Task<int> ExecuteAsync(
        RunnerCommandLine options,
        IHostExecutionCoordinator execution,
        IVisualStudioAttach debugger,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.AssemblyPath))
        {
            await Console.Error.WriteLineAsync($"Assembly not found: {options.AssemblyPath}").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!NUnitRunnerFilter.TryNormalize(options.Filter, out _, out var filterError))
        {
            await Console.Error.WriteLineAsync(filterError).ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        return options.UseGenericProtocol
            ? await RunGenericAsync(options, execution, debugger, cancellationToken).ConfigureAwait(false)
            : await RunLegacyNUnitAsync(options, execution, debugger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RunGenericAsync(
        RunnerCommandLine options,
        IHostExecutionCoordinator execution,
        IVisualStudioAttach debugger,
        CancellationToken cancellationToken)
    {
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
                    new TestingSelection([], options.Filter),
                    new Dictionary<string, string>()),
                progress,
                TimeSpan.FromSeconds(TestingHostTiming.HostPipeConnectTimeoutSeconds),
                debugger,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
            return await WriteExecutionFailureAsync(result).ConfigureAwait(false);

        Console.WriteLine(JsonSerializer.Serialize(result.Value, TestingJsonContext.Default.TestingRunResponse));
        return HasTestingFailure(result.Value!) ? RunnerExitCode.TestFailure : RunnerExitCode.Ok;
    }

    private static async Task<int> RunLegacyNUnitAsync(
        RunnerCommandLine options,
        IHostExecutionCoordinator execution,
        IVisualStudioAttach debugger,
        CancellationToken cancellationToken)
    {
        var result = await execution.ExecuteAsync(
                options.Context,
                debugger,
                async (pipe, requestCancellationToken) =>
                {
                    await using var client = await NUnitPipeClient.ConnectAsync(
                            pipe.PipeName,
                            TimeSpan.FromSeconds(TestingHostTiming.HostPipeConnectTimeoutSeconds),
                            requestCancellationToken)
                        .ConfigureAwait(false);
                    await client.HelloAsync(requestCancellationToken).ConfigureAwait(false);

                    var progress = new Progress<NUnitProgressEvent>(evt =>
                    {
                        Console.Error.WriteLine($"[progress] {evt.Case.Name} -> {evt.Case.Outcome}");
                    });
                    return await client.RunAsync(
                        options.AssemblyPath,
                        options.Filter,
                        progress,
                        requestCancellationToken).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
            return await WriteExecutionFailureAsync(result).ConfigureAwait(false);

        Console.WriteLine(JsonSerializer.Serialize(result.Value, NUnitJsonContext.Default.NUnitRunResponse));
        var response = result.Value!;
        return response.Summary.Failed > 0
            || response.Summary.Errors > 0
            || response.Summary.Cancelled > 0
            ? RunnerExitCode.TestFailure
            : RunnerExitCode.Ok;
    }

    private static async Task<int> WriteExecutionFailureAsync<T>(HostExecutionResult<T> result)
    {
        await Console.Error.WriteLineAsync(result.Error ?? "Host execution failed.").ConfigureAwait(false);
        return result.Failure switch
        {
            HostExecutionFailure.InvalidHost => RunnerExitCode.CliError,
            HostExecutionFailure.TimedOut => RunnerExitCode.HostTimeout,
            _ => RunnerExitCode.NoHost,
        };
    }

    private static bool HasTestingFailure(TestingRunResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.DiagnosticCode))
            return true;

        if (response.CancellationState is TestingCancellationState.Completed
            or TestingCancellationState.Poisoned)
            return true;

        return response.Results.Any(result =>
            string.Equals(result.Outcome, NUnitOutcomes.Failed, StringComparison.Ordinal)
            || string.Equals(result.Outcome, NUnitOutcomes.Error, StringComparison.Ordinal)
            || string.Equals(result.Outcome, NUnitOutcomes.Cancelled, StringComparison.Ordinal));
    }
}
