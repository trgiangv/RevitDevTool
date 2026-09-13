using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public sealed class ProcessTestRunnerClient : ITestRunnerTransport
{
    private readonly string _runnerPath;
    private readonly Lock _processLock = new();
    private Process? _activeProcess;
    private Guid? _activeRunId;

    public ProcessTestRunnerClient(string runnerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runnerPath);
        _runnerPath = runnerPath;
    }

    public TestRunResponse Run(
        TestRunRequest request,
        TestHostOptions hostOptions,
        Action<TestEvent> onEvent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hostOptions);
        ArgumentNullException.ThrowIfNull(onEvent);

        if (!TestingProtocol.IsCompatible(request.ProtocolVersion))
        {
            return new TestRunResponse(
                request.RunId,
                request.FrameworkId,
                GenerationId: null,
                Results: [],
                CancellationState: TestCancellationState.None,
                DiagnosticCode: TestingProtocol.IncompatibleCode,
                DiagnosticMessage: TestingProtocol.CreateUnsupportedMessage(request.ProtocolVersion));
        }

        var json = TestRunnerCli.SerializeExecute(request, hostOptions);
        return RunProcess(hostOptions, json, request.RunId, onEvent);
    }

    public void Cancel(Guid runId)
    {
        // Signal only. The in-flight Run WaitForExit collects the process.
        TestCancelSignal.TrySignal(runId);
    }

    public void Dispose()
    {
        Guid? runId;
        Process? process;
        lock (_processLock)
        {
            runId = _activeRunId;
            process = _activeProcess;
            _activeProcess = null;
            _activeRunId = null;
        }

        if (runId is { } id)
            TestCancelSignal.TrySignal(id);

        try
        {
            if (process is { HasExited: false })
                process.WaitForExit(TestHostTiming.CancelDetachWaitMilliseconds);
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }

        TryTerminate(process, waitForExit: false);
    }

    private TestRunResponse RunProcess(
        TestHostOptions hostOptions,
        string stdin,
        Guid runId,
        Action<TestEvent> onEvent)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _runnerPath,
            Arguments = TestRunnerCli.RunCommand,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        if (!process.Start())
            throw new InvalidOperationException("Could not start the DevTools TestRunner process.");

        lock (_processLock)
        {
            _activeProcess = process;
            _activeRunId = runId;
        }

        try
        {
            return ReadProcessResult(process, hostOptions, stdin, runId, onEvent);
        }
        finally
        {
            ClearActive(process);
        }
    }

    private static TestRunResponse ReadProcessResult(
        Process process,
        TestHostOptions hostOptions,
        string stdin,
        Guid runId,
        Action<TestEvent> onEvent)
    {
        var parsed = new RunnerOutputState();
        var stdoutTask = Task.Run(() => DrainStdout(process, onEvent, parsed));
        var stderrTask = process.StandardError.ReadToEndAsync();
        WriteStdin(process, stdin);
        WaitForRunnerExit(process, hostOptions, stdoutTask, stderrTask);
        DrainOutput(stdoutTask, stderrTask);
        var response = RequireResponse(parsed, process, stderrTask.Result, onEvent);
        EnsureSameRun(response, runId);
        ReplayResponseCasesIfNoEvents(parsed, onEvent);
        return response;
    }

    private static void DrainStdout(Process process, Action<TestEvent> onEvent, RunnerOutputState parsed)
    {
        while (process.StandardOutput.ReadLine() is { } line)
        {
            parsed.Buffer.AppendLine(line);
            ConsumeStdoutLine(line, onEvent, parsed);
        }
    }

    private static void WriteStdin(Process process, string stdin)
    {
        var stdinBytes = Encoding.UTF8.GetBytes(stdin);
        process.StandardInput.BaseStream.Write(stdinBytes, 0, stdinBytes.Length);
        process.StandardInput.BaseStream.Flush();
        process.StandardInput.Close();
    }

    private static void WaitForRunnerExit(
        Process process,
        TestHostOptions hostOptions,
        Task stdoutTask,
        Task<string> stderrTask)
    {
        var timeoutMs = TestHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            hostOptions.LaunchTimeoutSeconds,
            hostOptions.EffectiveRequestTimeoutSeconds) * 1000;
        if (process.WaitForExit(timeoutMs))
            return;

        try
        {
            TryTerminate(process, waitForExit: false);
        }
        catch
        {
            // Best effort.
        }

        Task.WaitAll([stdoutTask, stderrTask], TestHostTiming.TimedOutProcessOutputDrainMilliseconds);
        var stderr = stderrTask.IsCompleted ? stderrTask.Result : string.Empty;
        var detail = string.IsNullOrWhiteSpace(stderr) ? string.Empty : $"{Environment.NewLine}{stderr.Trim()}";
        throw new TimeoutException(
            $"The DevTools TestRunner process did not finish within {timeoutMs / 1000}s.{detail}");
    }

    private static void DrainOutput(Task stdoutTask, Task stderrTask)
    {
        if (!Task.WaitAll([stdoutTask, stderrTask], TestHostTiming.ExitedProcessOutputDrainMilliseconds))
            throw new TimeoutException("Timed out reading TestRunner output.");
    }

    private static TestRunResponse RequireResponse(
        RunnerOutputState parsed,
        Process process,
        string stderrOutput,
        Action<TestEvent> onEvent)
    {
        if (parsed.Response is null)
            ConsumeStdoutLine(parsed.Buffer.ToString(), onEvent, parsed);

        if (parsed.Response is not null)
            return parsed.Response;

        var details = string.IsNullOrWhiteSpace(stderrOutput)
            ? $"TestRunner process exited with code {process.ExitCode}."
            : stderrOutput.Trim();
        throw new InvalidOperationException(details);
    }

    private static void EnsureSameRun(TestRunResponse response, Guid runId)
    {
        if (response.RunId == runId)
            return;

        throw new InvalidOperationException(
            $"TestRunner returned RunId '{response.RunId}' but the adapter sent '{runId}'.");
    }

    private static void ReplayResponseCasesIfNoEvents(RunnerOutputState parsed, Action<TestEvent> onEvent)
    {
        if (parsed.SawEvent || parsed.Response is null)
            return;

        foreach (var result in parsed.Response.Results)
        {
            onEvent(new TestEvent(
                parsed.Response.RunId,
                TestEventKinds.Case,
                result,
                null,
                null,
                TestCancellationState.None));
        }
    }

    private sealed class RunnerOutputState
    {
        public TestRunResponse? Response;
        public bool SawEvent;
        public StringBuilder Buffer { get; } = new();
    }

    private static void ConsumeStdoutLine(
        string line,
        Action<TestEvent> onEvent,
        RunnerOutputState state)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (!TryReadStreamMessage(line, out var message) || message is null)
            return;

        if (message.Event is { } testingEvent)
        {
            state.SawEvent = true;
            onEvent(testingEvent);
        }

        if (message.Response is { } streamed)
            state.Response = streamed;
    }

    private static bool TryReadStreamMessage(string json, out TestRunnerStreamMessage? message)
    {
        message = null;
        try
        {
            message = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunnerStreamMessage);
            return message is { Event: not null } or { Response: not null };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void ClearActive(Process process)
    {
        lock (_processLock)
        {
            if (!ReferenceEquals(_activeProcess, process)) return;
            _activeProcess = null;
            _activeRunId = null;
        }
    }

    private static void TryTerminate(Process? process, bool waitForExit)
    {
        try
        {
            if (process is not { HasExited: false })
                return;

            if (waitForExit && process.WaitForExit(2_000))
                return;

            process.Kill();
        }
        catch (ObjectDisposedException)
        {
            // Process was never started or already disposed.
        }
        catch (InvalidOperationException)
        {
            // Process exited between the state check and the operation.
        }
    }
}
