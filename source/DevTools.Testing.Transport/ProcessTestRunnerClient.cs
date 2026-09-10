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
        return RunProcess(hostOptions, [TestRunnerCli.RunCommand], json, request.RunId, onEvent);
    }

    public void Cancel(Guid runId)
    {
        TestCancelSignal.TrySignal(runId);
        Process? process;
        lock (_processLock)
        {
            if (_activeRunId != runId)
                return;
            process = _activeProcess;
        }

        try
        {
            if (process is not { HasExited: false })
                return;

            if (!process.WaitForExit(2_000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Best effort after cooperative cancel.
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Process was never started or already disposed.
        }
        catch (InvalidOperationException)
        {
            // ignore
        }
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
                process.Kill();
        }
        catch
        {
            // Best effort.
        }
    }

    private TestRunResponse RunProcess(
        TestHostOptions hostOptions,
        IReadOnlyList<string> arguments,
        string stdin,
        Guid runId,
        Action<TestEvent> onEvent)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _runnerPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
            AddArgument(startInfo, argument);

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        lock (_processLock)
        {
            _activeProcess = process;
            _activeRunId = runId;
        }

        try
        {
            return ReadRun(process, hostOptions, stdin, runId, onEvent);
        }
        finally
        {
            ClearActive(process);
        }
    }

    private static TestRunResponse ReadRun(
        Process process,
        TestHostOptions hostOptions,
        string stdin,
        Guid runId,
        Action<TestEvent> onEvent)
    {
        var parsed = new StdoutParseState();
        var stdoutTask = Task.Run(() => DrainStdout(process, onEvent, parsed));
        var stderrTask = process.StandardError.ReadToEndAsync();
        WriteStdin(process, stdin);
        WaitForRunnerExit(process, hostOptions, stdoutTask, stderrTask);
        DrainOutput(stdoutTask, stderrTask);
        var response = RequireResponse(parsed, process, stderrTask.Result, onEvent);
        EnsureSameRun(response, runId);
        ReplayIfBatchOnly(parsed, onEvent);
        return response;
    }

    private static void DrainStdout(Process process, Action<TestEvent> onEvent, StdoutParseState parsed)
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
            process.Kill();
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
        StdoutParseState parsed,
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

    private static void ReplayIfBatchOnly(StdoutParseState parsed, Action<TestEvent> onEvent)
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

    private sealed class StdoutParseState
    {
        public TestRunResponse? Response;
        public bool SawEvent;
        public StringBuilder Buffer { get; } = new();
    }

    private static void ConsumeStdoutLine(
        string line,
        Action<TestEvent> onEvent,
        StdoutParseState state)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (TryReadStreamMessage(line, out var message) && message is not null)
        {
            if (message.Event is { } testingEvent)
            {
                state.SawEvent = true;
                onEvent(testingEvent);
            }

            if (message.Response is { } streamed)
                state.Response = streamed;
        }
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

    private static void AddArgument(ProcessStartInfo startInfo, string argument)
    {
#if NETFRAMEWORK || NETSTANDARD
        if (startInfo.Arguments.Length > 0)
            startInfo.Arguments += " ";
        startInfo.Arguments += QuoteArgument(argument);
#else
        startInfo.ArgumentList.Add(argument);
#endif
    }

#if NETFRAMEWORK || NETSTANDARD
    private static string QuoteArgument(string value)
    {
        if (value.Length == 0 || value.IndexOfAny([' ', '\t', '"']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
#endif
}
