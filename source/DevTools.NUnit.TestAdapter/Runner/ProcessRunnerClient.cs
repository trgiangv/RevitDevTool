using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.TestAdapter.Runner;

public sealed class ProcessRunnerClient : IRunnerClient, IDisposable
{
    private static readonly JsonSerializerOptions WireJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string _runnerPath;
    private Process? _activeProcess;
    private readonly Lock _processLock = new();

    public ProcessRunnerClient(string runnerPath)
    {
        if (string.IsNullOrWhiteSpace(runnerPath))
            throw new ArgumentException("Runner path is required.", nameof(runnerPath));

        _runnerPath = runnerPath;
    }

    public IReadOnlyList<RemoteTestCase> Discover(string source, RunnerHostOptions options)
    {
        var output = RunRunner(options, [.. BuildHostArguments("discover", source, options)]);

        var response = JsonSerializer.Deserialize<NUnitDiscoverResponse>(output, WireJsonOptions)
            ?? throw new InvalidOperationException("Runner discover returned empty JSON.");

        return response.Cases
            .Select(test => new RemoteTestCase(test.Id, test.Name, test.FullName, source))
            .ToList();
    }

    public RemoteRunResult Run(
        string source,
        string? filter,
        RunnerHostOptions options)
    {
        var args = BuildHostArguments("run", source, options);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            args.Add("--filter");
            args.Add(filter!);
        }

        var output = RunRunner(options, [.. args]);
        var response = JsonSerializer.Deserialize<NUnitRunResponse>(output, WireJsonOptions)
            ?? throw new InvalidOperationException("Runner run returned empty JSON.");

        return new RemoteRunResult(response.Cases.Select(MapCase).ToList());
    }

    public void Cancel()
    {
        lock (_processLock)
        {
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
            }
        }
    }

    public void Dispose() => Cancel();

    private static List<string> BuildHostArguments(string command, string source, RunnerHostOptions options)
    {
        var hostTimeoutSeconds = options.HostTimeoutSeconds;

        var args = new List<string>
        {
            command,
            source,
            "--host",
            options.Host,
            "--version",
            options.HostVersion,
            "--host-timeout",
            hostTimeoutSeconds.ToString(),
            "--host-launch-timeout",
            options.HostLaunchTimeoutSeconds.ToString(),
        };

        if (options.HostLaunch)
            args.Add("--host-launch");

        return args;
    }

    private string RunRunner(RunnerHostOptions options, params string[] arguments)
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
            startInfo.ArgumentList.Add(argument);

        using var process = new Process();
        process.StartInfo = startInfo;
        lock (_processLock)
            _activeProcess = process;

        process.Start();

        // Read both streams in parallel to avoid deadlock when runner writes progress to stderr.
        var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());

        var timeoutMs = ComputeRunnerTimeoutMs(options);
        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Best effort — process may already be gone.
            }

            // Bounded drain after kill so a wedged reader cannot hang the adapter forever.
            // Parallel ReadToEnd on stdout+stderr is correct (avoids classic Process deadlock);
            // unbounded WaitAll after Kill is what could stall the adapter.
            _ = Task.WaitAll([stdoutTask, stderrTask], 5_000);
            lock (_processLock)
                _activeProcess = null;

            var stderr = stdoutTask.IsCompleted && stderrTask.IsCompleted
                ? stderrTask.Result
                : string.Empty;
            var detail = string.IsNullOrWhiteSpace(stderr) ? string.Empty : $"{Environment.NewLine}{stderr.Trim()}";
            throw new TimeoutException(
                $"DevTools.NUnit.Runner did not exit within {timeoutMs / 1000}s.{detail}");
        }

        if (!Task.WaitAll([stdoutTask, stderrTask], 30_000))
        {
            lock (_processLock)
                _activeProcess = null;
            throw new TimeoutException("Timed out draining DevTools.NUnit.Runner stdout/stderr.");
        }

        var stdout = stdoutTask.Result;
        var stderrOutput = stderrTask.Result;

        lock (_processLock)
            _activeProcess = null;

        if (string.IsNullOrWhiteSpace(stdout))
        {
            var details = string.IsNullOrWhiteSpace(stderrOutput)
                ? $"Runner exited with code {process.ExitCode}."
                : stderrOutput.Trim();
            throw new InvalidOperationException(details);
        }

        return stdout.Trim();
    }

    private static int ComputeRunnerTimeoutMs(RunnerHostOptions options) =>
        NUnitHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            options.HostLaunchTimeoutSeconds,
            options.HostTimeoutSeconds) * 1000;

    private static RemoteTestCaseResult MapCase(NUnitCaseResult result) =>
        new(
            result.Name,
            result.Outcome,
            result.DurationMs,
            result.Message,
            result.StackTrace,
            result.Output);
}
