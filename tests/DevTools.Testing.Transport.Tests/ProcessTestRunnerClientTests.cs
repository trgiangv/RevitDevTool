using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class ProcessTestRunnerClientTests
{
    [Fact]
    public void Run_invokes_fake_executable_with_framework_and_host_arguments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools", "TestingTransport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var argsPath = Path.Combine(directory, "args.txt");
        var runnerPath = Path.Combine(directory, "fake-runner.cmd");
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var response = new TestingRunResponse(
            runId,
            TestingFrameworkIds.NUnit,
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
        File.WriteAllText(runnerPath, $"""
            @echo off
            echo %* > "{argsPath}"
            type "{Path.Combine(directory, "response.json")}"
            """);

        var observed = new List<TestingCaseResult>();
        using var client = new ProcessTestRunnerClient(runnerPath);
        var result = client.Run(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                TestingFrameworkIds.NUnit,
                new TestingAssemblyReference(@"C:\tests\Sample.dll", "net10.0-windows", "hash"),
                new TestingSelection(["opaque-id"]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2025", false, 60, 180, runnerPath),
            observed.Add);

        Assert.Equal("gen-1", result.GenerationId);
        Assert.Single(observed);
        Assert.Equal("opaque-id", observed[0].TestId);
        var captured = File.ReadAllText(argsPath);
        Assert.Contains("--framework", captured, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nunit", captured, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--host", captured, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--test", captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discover", captured, StringComparison.OrdinalIgnoreCase);
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
                TestingFrameworkIds.Xunit,
                new TestingAssemblyReference(@"C:\tests\Sample.dll", null, null),
                new TestingSelection([]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2025", false, 60, 180, runnerPath),
            _ => throw new InvalidOperationException("onResult must not run for a protocol mismatch."));

        Assert.Equal(TestingProtocol.IncompatibleCode, result.DiagnosticCode);
        Assert.Equal(TestingCancellationState.None, result.CancellationState);
    }

    [Fact]
    public void ITestRunnerTransport_has_no_discover_method()
    {
        Assert.DoesNotContain(
            typeof(ITestRunnerTransport).GetMethods(),
            static method => method.Name.Contains("Discover", StringComparison.OrdinalIgnoreCase));
    }
}
