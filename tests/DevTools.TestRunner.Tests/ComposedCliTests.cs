using System.Diagnostics;
using DevTools.TestRunner.Core.Parsing;

namespace DevTools.TestRunner.Tests;

public sealed class ComposedCliTests
{
    [Fact]
    public async Task Run_missing_assembly_exits_before_host_contact()
    {
        var result = await RunAsync(
            FindRunnerPath(),
            [
                "run",
                Path.Combine(Path.GetTempPath(), "missing-devtools-tests.dll"),
                "--host", "Revit",
                "--host-version", "2026",
                "--framework", "nunit",
            ]);

        Assert.Equal(RunnerExitCode.CliError, result.ExitCode);
        Assert.Contains("Assembly not found", result.StandardError, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string StandardError)> RunAsync(string executable, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start TestRunner.");
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
