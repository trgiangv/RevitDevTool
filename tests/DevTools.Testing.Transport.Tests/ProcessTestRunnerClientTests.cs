using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class ProcessTestRunnerClientTests
{
    [Fact]
    public void Run_invokes_machine_run_with_full_invocation_json()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools", "TestingTransport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var argsPath = Path.Combine(directory, "args.txt");
        var stdinPath = Path.Combine(directory, "stdin.json");
        var capturePath = Path.Combine(directory, "capture.ps1");
        var runnerPath = Path.Combine(directory, "fake-runner.cmd");
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var response = new TestingRunResponse(
            runId,
            "provider.example",
            "gen-1",
            [
                new TestingCaseResult(
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
            TestingCancellationState.None,
            null,
            null);
        var json = JsonSerializer.Serialize(response, TestingJsonContext.Default.TestingRunResponse);
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

        var observed = new List<TestingEvent>();
        using var client = new ProcessTestRunnerClient(runnerPath);
        var result = client.Run(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                "provider.example",
                new TestingAssemblyReference(@"C:\tests\Sample.dll"),
                TestingSelection.FromTestIds(["opaque-id"])),
            new TestingHostOptions("Revit", "2025", false, 60, 180, runnerPath),
            observed.Add);

        Assert.Equal("gen-1", result.GenerationId);
        Assert.Single(observed);
        Assert.Equal("opaque-id", observed[0].Case!.TestId);
        var captured = File.ReadAllText(argsPath);
        Assert.Contains(TestingRunnerCli.MachineRunCommand, captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--framework", captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--test", captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discover", captured, StringComparison.OrdinalIgnoreCase);

        var stdin = File.ReadAllText(stdinPath);
        var invocation = JsonSerializer.Deserialize(stdin, TestingJsonContext.Default.TestingRunInvocation);
        Assert.NotNull(invocation);
        Assert.Equal(TestingProtocol.CurrentVersion, invocation.ProtocolVersion);
        Assert.Equal(runId, invocation.Run.RunId);
        Assert.Equal("provider.example", invocation.Run.FrameworkId);
        Assert.Equal(TestingSelectionKind.TestIds, invocation.Run.Selection.Kind);
        Assert.Equal("Revit", invocation.Host.HostName);
        Assert.Equal(60, invocation.Host.PerTestTimeoutSeconds);
        Assert.Null(invocation.Host.FrameworkId);
        Assert.Null(invocation.Host.RunnerPath);
    }

    [Fact]
    public void Run_returns_protocol_mismatch_without_starting_the_executable()
    {
        var runnerPath = Path.Combine(Path.GetTempPath(), "missing-devtools-testrunner.exe");
        using var client = new ProcessTestRunnerClient(runnerPath);
        var result = client.Run(
            new TestingRunRequest(
                1,
                Guid.NewGuid(),
                "future-provider",
                new TestingAssemblyReference(@"C:\tests\Sample.dll"),
                TestingSelection.All),
            new TestingHostOptions("Revit", "2025", false, 60, 180, runnerPath),
            _ => throw new InvalidOperationException("onEvent must not run for a protocol mismatch."));

        Assert.Equal(TestingProtocol.IncompatibleCode, result.DiagnosticCode);
        Assert.Equal(TestingCancellationState.None, result.CancellationState);
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
