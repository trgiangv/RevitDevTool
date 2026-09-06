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
        Action<TestingCaseResult> onResult)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hostOptions);
        ArgumentNullException.ThrowIfNull(onResult);

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

        var output = RunProcess(hostOptions, TestingRunnerCli.BuildRunArguments(request, hostOptions), request.RunId);
        var response = JsonSerializer.Deserialize(output, TestingJsonContext.Default.TestingRunResponse)
            ?? throw new InvalidOperationException("TestRunner run returned empty JSON.");

        foreach (var result in response.Results)
            onResult(result);

        return response;
    }

    public void Cancel(Guid runId)
    {
        lock (_processLock)
        {
            if (_activeRunId != runId)
                return;

            try
            {
                if (_activeProcess is { HasExited: false })
                    _activeProcess.Kill();
            }
            catch
            {
                // Best effort.
            }
            finally
            {
                _activeProcess?.Dispose();
                _activeProcess = null;
                _activeRunId = null;
            }
        }
    }

    public void Dispose() => Cancel(_activeRunId ?? Guid.Empty);

    private string RunProcess(TestingHostOptions hostOptions, IReadOnlyList<string> arguments, Guid runId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _runnerPath,
            UseShellExecute = false,
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
        lock (_processLock)
        {
            _activeProcess = process;
            _activeRunId = runId;
        }

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var timeoutMs = TestingHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            hostOptions.LaunchTimeoutSeconds,
            hostOptions.PerTestTimeoutSeconds) * 1000;

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
            ClearActive(process);
            var stderr = stderrTask.IsCompleted ? stderrTask.Result : string.Empty;
            var detail = string.IsNullOrWhiteSpace(stderr) ? string.Empty : $"{Environment.NewLine}{stderr.Trim()}";
            throw new TimeoutException(
                $"The DevTools TestRunner process did not finish within {timeoutMs / 1000}s.{detail}");
        }

        if (!Task.WaitAll([stdoutTask, stderrTask], TestingHostTiming.ExitedProcessOutputDrainMilliseconds))
        {
            ClearActive(process);
            throw new TimeoutException("Timed out reading TestRunner output.");
        }

        var stdout = stdoutTask.Result;
        var stderrOutput = stderrTask.Result;
        ClearActive(process);

        if (string.IsNullOrWhiteSpace(stdout))
        {
            var details = string.IsNullOrWhiteSpace(stderrOutput)
                ? $"TestRunner process exited with code {process.ExitCode}."
                : stderrOutput.Trim();
            throw new InvalidOperationException(details);
        }

        return stdout.Trim();
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
        if (value.Length > 0 && value.IndexOfAny([' ', '\t', '"']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
#endif
}
