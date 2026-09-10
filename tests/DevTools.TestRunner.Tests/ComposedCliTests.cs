using System.Diagnostics;
using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Parsing;

namespace DevTools.TestRunner.Tests;

public sealed class ComposedCliTests
{
    [Fact]
    public async Task Run_missing_assembly_exits_before_host_contact()
    {
        var execute = new TestRunExecute(
            TestingProtocol.CurrentVersion,
            new TestHostOptions("Revit", "2026", false, 60, 180),
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(Path.Combine(Path.GetTempPath(), "missing-devtools-tests.dll")),
                TestSelection.All));
        var json = JsonSerializer.Serialize(execute, TestingJsonContext.Default.TestRunExecute);
        var result = await RunAsync(FindRunnerPath(), ["run"], json);

        Assert.Equal(RunnerExitCode.CliError, result.ExitCode);
        Assert.Contains("Assembly not found", result.StandardError, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string StandardError)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string stdin)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start TestRunner.");
        await process.StandardInput.WriteAsync(stdin);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        _ = await stdout;
        return (process.ExitCode, await stderr);
    }

    private static string FindRunnerPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var runner = Path.Combine(directory.FullName, "source", "DevTools.TestRunner", "bin", "Debug", "net10.0-windows", "win-x64", "DevTools.TestRunner.exe");
            if (File.Exists(runner))
                return runner;
        }
        throw new FileNotFoundException("DevTools.TestRunner.exe was not built.");
    }
}
