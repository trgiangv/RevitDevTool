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

    public TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingEvent> onEvent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hostOptions);
        ArgumentNullException.ThrowIfNull(onEvent);

        if (!TestingProtocol.IsCompatible(request.ProtocolVersion))
        {
            return new TestingRunResponse(
                request.RunId,
                request.FrameworkId,
                GenerationId: null,
                Results: [],
                CancellationState: TestingCancellationState.None,
                DiagnosticCode: TestingProtocol.IncompatibleCode,
                DiagnosticMessage: TestingProtocol.CreateUnsupportedMessage(request.ProtocolVersion));
        }

        var json = TestingRunnerCli.SerializeInvocation(request, hostOptions);
        return RunProcess(hostOptions, [TestingRunnerCli.MachineRunCommand], json, request.RunId, onEvent);
    }

    public void Cancel(Guid runId)
    {
        TestingCancelSignal.TrySignal(runId);
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
            TestingCancelSignal.TrySignal(id);

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

    private TestingRunResponse RunProcess(
        TestingHostOptions hostOptions,
        IReadOnlyList<string> arguments,
        string stdin,
        Guid runId,
        Action<TestingEvent> onEvent)
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

    private static TestingRunResponse ReadRun(
        Process process,
        TestingHostOptions hostOptions,
        string stdin,
        Guid runId,
        Action<TestingEvent> onEvent)
    {
        var parsed = new StdoutParseState();
        var stdoutTask = Task.Run(() =>
        {
            while (process.StandardOutput.ReadLine() is { } line)
            {
                parsed.Buffer.AppendLine(line);
                ConsumeStdoutLine(line, onEvent, parsed);
            }
        });
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdinBytes = Encoding.UTF8.GetBytes(stdin);
        process.StandardInput.BaseStream.Write(stdinBytes, 0, stdinBytes.Length);
        process.StandardInput.BaseStream.Flush();
        process.StandardInput.Close();

        var timeoutMs = TestingHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            hostOptions.LaunchTimeoutSeconds,
            hostOptions.EffectiveRequestTimeoutSeconds) * 1000;

        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Best effort.
            }

            Task.WaitAll([stdoutTask, stderrTask], TestingHostTiming.TimedOutProcessOutputDrainMilliseconds);
            var stderr = stderrTask.IsCompleted ? stderrTask.Result : string.Empty;
            var detail = string.IsNullOrWhiteSpace(stderr) ? string.Empty : $"{Environment.NewLine}{stderr.Trim()}";
            throw new TimeoutException(
                $"The DevTools TestRunner process did not finish within {timeoutMs / 1000}s.{detail}");
        }

        if (!Task.WaitAll([stdoutTask, stderrTask], TestingHostTiming.ExitedProcessOutputDrainMilliseconds))
            throw new TimeoutException("Timed out reading TestRunner output.");

        var stderrOutput = stderrTask.Result;

        if (parsed.Response is null)
            ConsumeStdoutLine(parsed.Buffer.ToString(), onEvent, parsed);

        if (parsed.Response is null)
        {
            var details = string.IsNullOrWhiteSpace(stderrOutput)
                ? $"TestRunner process exited with code {process.ExitCode}."
                : stderrOutput.Trim();
            throw new InvalidOperationException(details);
        }

        if (parsed.Response.RunId != runId)
        {
            throw new InvalidOperationException(
                $"TestRunner returned RunId '{parsed.Response.RunId}' but the adapter sent '{runId}'.");
        }

        if (!parsed.SawEvent)
        {
            foreach (var result in parsed.Response.Results)
            {
                onEvent(new TestingEvent(
                    parsed.Response.RunId,
                    TestingEventKinds.Case,
                    result,
                    null,
                    null,
                    TestingCancellationState.None));
            }
        }

        return parsed.Response;
    }

    private sealed class StdoutParseState
    {
        public TestingRunResponse? Response;
        public bool SawEvent;
        public StringBuilder Buffer { get; } = new();
    }

    private static void ConsumeStdoutLine(
        string line,
        Action<TestingEvent> onEvent,
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
            return;
        }

        if (TryReadResponse(line, out var parsed) && parsed is not null)
            state.Response = parsed;
    }

    private static bool TryReadStreamMessage(string json, out TestingRunnerStreamMessage? message)
    {
        message = null;
        try
        {
            message = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunnerStreamMessage);
            return message is { Event: not null } or { Response: not null };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadResponse(string json, out TestingRunResponse? response)
    {
        response = null;
        try
        {
            response = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunResponse);
            return response is not null;
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
