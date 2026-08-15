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

    public IReadOnlyList<RemoteTestCase> Discover(
        string assemblyPath,
        RunnerHostOptions options,
        RunnerTestFilter filter)
    {
        var output = RunRunner(options, BuildHostArguments(NUnitRunnerCli.DiscoverCommand, assemblyPath, options, filter));
        var response = JsonSerializer.Deserialize<NUnitDiscoverResponse>(output, WireJsonOptions)
            ?? throw new InvalidOperationException("Runner discover returned empty JSON.");
        return response.Cases
            .Select(test => new RemoteTestCase(test.Id, test.Name, test.FullName, assemblyPath))
            .ToList();
    }

    public RemoteRunResult Run(
        string assemblyPath,
        RunnerHostOptions options,
        RunnerTestFilter filter)
    {
        var output = RunRunner(options, BuildHostArguments(NUnitRunnerCli.RunCommand, assemblyPath, options, filter));
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

    internal static IReadOnlyList<string> BuildHostArguments(
        string command,
        string source,
        RunnerHostOptions options,
        RunnerTestFilter filter) =>
        NUnitRunnerCli.BuildArguments(
            command,
            source,
            options.Host,
            options.HostVersion,
            options.HostTimeoutSeconds,
            options.HostLaunchTimeoutSeconds,
            options.HostLaunch,
            filter.Names,
            filter.FullNames,
            debugParentPid: command == NUnitRunnerCli.RunCommand ? options.DebugParentPid : null);

    internal static string ResolveRunnerPath(RunnerHostOptions options)
    {
        if (IsRunnable(options.RunnerPath))
            return Path.GetFullPath(options.RunnerPath!);

        var bundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "ApplicationPlugins",
            "RevitDevTool.bundle",
            "Contents",
            "DevTools.NUnit.Runner.exe");
        if (IsRunnable(bundlePath))
            return bundlePath;

        throw new InvalidOperationException(
            "RevitDevTool is not installed. Install it from https://github.com/trgiangv/RevitDevTool");
    }

    private string RunRunner(RunnerHostOptions options, IReadOnlyList<string> arguments)
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
            _activeProcess = process;

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var timeoutMs = NUnitHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            options.HostLaunchTimeoutSeconds,
            options.HostTimeoutSeconds) * 1000;

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

            _ = Task.WaitAll([stdoutTask, stderrTask], 5_000);
            lock (_processLock)
                _activeProcess = null;

            var stderr = stdoutTask.IsCompleted && stderrTask.IsCompleted
                ? stderrTask.Result
                : string.Empty;
            var detail = string.IsNullOrWhiteSpace(stderr) ? string.Empty : $"{Environment.NewLine}{stderr.Trim()}";
            throw new TimeoutException(
                $"The RevitDevTool host test run did not finish within {timeoutMs / 1000}s.{detail}");
        }

        if (!Task.WaitAll([stdoutTask, stderrTask], 30_000))
        {
            lock (_processLock)
                _activeProcess = null;
            throw new TimeoutException("Timed out reading host test output.");
        }

        var stdout = stdoutTask.Result;
        var stderrOutput = stderrTask.Result;

        lock (_processLock)
            _activeProcess = null;

        if (string.IsNullOrWhiteSpace(stdout))
        {
            var details = string.IsNullOrWhiteSpace(stderrOutput)
                ? $"Host test process exited with code {process.ExitCode}."
                : stderrOutput.Trim();
            throw new InvalidOperationException(details);
        }

        return stdout.Trim();
    }

    private static bool IsRunnable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static void AddArgument(ProcessStartInfo startInfo, string argument)
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
    private static string QuoteArgument(string value)
    {
        if (value.Length > 0 && value.IndexOfAny([' ', '\t', '"']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
#endif

    private static RemoteTestCaseResult MapCase(NUnitCaseResult result) =>
        new(
            result.Name,
            result.Outcome,
            result.DurationMs,
            result.Message,
            result.StackTrace,
            result.Output);
}
