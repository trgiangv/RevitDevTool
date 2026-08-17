using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public sealed class ProcessTestRunnerClient : ITestRunnerTransport
{
    readonly string _runnerPath;
    readonly object _processLock = new();
    Process? _activeProcess;
    Guid? _activeRunId;

    public ProcessTestRunnerClient(string runnerPath)
    {
        if (string.IsNullOrWhiteSpace(runnerPath))
            throw new ArgumentException("Runner path is required.", nameof(runnerPath));

        _runnerPath = runnerPath;
    }

    public TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingCaseResult> onResult)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (hostOptions is null)
            throw new ArgumentNullException(nameof(hostOptions));
        if (onResult is null)
            throw new ArgumentNullException(nameof(onResult));

        if (!TestingProtocolBridge.IsCompatible(request.ProtocolVersion))
        {
            return new TestingRunResponse(
                request.RunId,
                request.FrameworkId,
                GenerationId: null,
                Results: [],
                CancellationState: TestingCancellationState.None,
                DiagnosticCode: TestingProtocol.IncompatibleCode,
                DiagnosticMessage: TestingProtocolBridge.CreateMessage(request.ProtocolVersion));
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

    string RunProcess(TestingHostOptions hostOptions, IReadOnlyList<string> arguments, Guid runId)
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

        using var process = new Process { StartInfo = startInfo };
        lock (_processLock)
        {
            _activeProcess = process;
            _activeRunId = runId;
        }

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var timeoutMs = TestingHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            hostOptions.HostLaunchTimeoutSeconds,
            hostOptions.HostTimeoutSeconds) * 1000;

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

    void ClearActive(Process process)
    {
        lock (_processLock)
        {
            if (ReferenceEquals(_activeProcess, process))
            {
                _activeProcess = null;
                _activeRunId = null;
            }
        }
    }

    static void AddArgument(ProcessStartInfo startInfo, string argument)
    {
#if NETFRAMEWORK
        if (startInfo.Arguments.Length > 0)
            startInfo.Arguments += " ";
        startInfo.Arguments += QuoteArgument(argument);
#else
        startInfo.ArgumentList.Add(argument);
#endif
    }

#if NETFRAMEWORK
    static string QuoteArgument(string value)
    {
        if (value.Length > 0 && value.IndexOfAny([' ', '\t', '"']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
#endif
}
