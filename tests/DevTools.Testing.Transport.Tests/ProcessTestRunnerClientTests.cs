using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class ProcessTestRunnerClientTests
{
    [Fact]
    public void Run_invokes_run_with_full_execute_json()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools", "TestingTransport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var argsPath = Path.Combine(directory, "args.txt");
        var stdinPath = Path.Combine(directory, "stdin.json");
        var capturePath = Path.Combine(directory, "capture.ps1");
        var runnerPath = Path.Combine(directory, "fake-runner.cmd");
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var response = new TestRunResponse(
            runId,
            TestFrameworkId.NUnit,
            "gen-1",
            [
                new TestCaseResult(
                    "opaque-id",
                    "Pass",
                    "Passed",
                    1.5,
                    null,
                    null,
                    "ok",
                    null,
                    [],
                    []),
            ],
            TestCancellationState.None,
            null,
            null);
        var json = JsonSerializer.Serialize(
            new TestRunnerStreamMessage(Response: response),
            TestingJsonContext.Default.TestRunnerStreamMessage);
        File.WriteAllText(Path.Combine(directory, "response.json"), json);
        File.WriteAllText(capturePath, """
            $out = Join-Path $PSScriptRoot 'stdin.json'
            $stdin = [Console]::OpenStandardInput()
            $buffer = New-Object IO.MemoryStream
            $stdin.CopyTo($buffer)
            [IO.File]::WriteAllBytes($out, $buffer.ToArray())
            """);
        File.WriteAllText(runnerPath, $"""
            @echo off
            echo %* > "{argsPath}"
            powershell -NoProfile -File "{capturePath}"
            type "{Path.Combine(directory, "response.json")}"
            """);

        var observed = new List<TestEvent>();
        using var client = new ProcessTestRunnerClient(runnerPath);
        var result = client.Run(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                TestFrameworkId.NUnit,
                new TestAssemblyReference(@"C:\tests\Sample.dll"),
                TestSelection.FromTestIds(["opaque-id"])),
            new TestHostOptions("Revit", "2025", false, 60, 180),
            observed.Add);

        Assert.Equal("gen-1", result.GenerationId);
        Assert.Single(observed);
        Assert.Equal("opaque-id", observed[0].Case!.TestId);
        var captured = File.ReadAllText(argsPath);
        Assert.Contains(TestRunnerCli.RunCommand, captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--framework", captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--test", captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discover", captured, StringComparison.OrdinalIgnoreCase);

        var stdin = File.ReadAllText(stdinPath);
        var execute = JsonSerializer.Deserialize(stdin, TestingJsonContext.Default.TestRunExecute);
        Assert.NotNull(execute);
        Assert.Equal(TestingProtocol.CurrentVersion, execute.ProtocolVersion);
        Assert.Equal(runId, execute.Run.RunId);
        Assert.Equal(TestFrameworkId.NUnit, execute.Run.FrameworkId);
        Assert.Equal(TestSelectionKind.TestIds, execute.Run.Selection.Kind);
        Assert.Equal("Revit", execute.Host.HostName);
        Assert.Equal(60, execute.Host.PerTestTimeoutSeconds);
        Assert.DoesNotContain("runner_path", stdin, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_returns_protocol_mismatch_without_starting_the_executable()
    {
        var runnerPath = Path.Combine(Path.GetTempPath(), "missing-devtools-testrunner.exe");
        using var client = new ProcessTestRunnerClient(runnerPath);
        var result = client.Run(
            new TestRunRequest(
                1,
                Guid.NewGuid(),
                TestFrameworkId.TUnit,
                new TestAssemblyReference(@"C:\tests\Sample.dll"),
                TestSelection.All),
            new TestHostOptions("Revit", "2025", false, 60, 180),
            _ => throw new InvalidOperationException("onEvent must not run for a protocol mismatch."));

        Assert.Equal(TestingProtocol.IncompatibleCode, result.DiagnosticCode);
        Assert.Equal(TestCancellationState.None, result.CancellationState);
    }

    [Fact]
    public void Cancel_after_dispose_does_not_throw()
    {
        var client = new ProcessTestRunnerClient(
            Path.Combine(Path.GetTempPath(), "missing-devtools-testrunner.exe"));
        client.Dispose();
        client.Cancel(Guid.NewGuid());
    }

    [Fact]
    public void ITestRunnerTransport_has_no_discover_method()
    {
        Assert.DoesNotContain(
            typeof(ITestRunnerTransport).GetMethods(),
            static method => method.Name.Contains("Discover", StringComparison.OrdinalIgnoreCase));
    }
}
