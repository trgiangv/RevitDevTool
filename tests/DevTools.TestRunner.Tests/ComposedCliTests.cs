using System.Diagnostics;
using System.Text.Json;

namespace DevTools.TestRunner.Tests;

public sealed class ComposedCliTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Discover_uses_composed_executable_with_default_or_explicit_nunit_module(bool generic)
    {
        var arguments = new List<string>
        {
            "discover", typeof(ComposedCliTests).Assembly.Location,
            "--host", "Revit",
            "--host-version", "2026",
        };
        if (generic)
        {
            arguments.Add("--framework");
            arguments.Add("nunit");
        }

        var result = await RunAsync(FindRunnerPath(), arguments);

        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.True(json.RootElement.TryGetProperty("cases", out _));
    }

    private static async Task<(int ExitCode, string StandardOutput)> RunAsync(string executable, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start TestRunner.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, await stderr);
        return (process.ExitCode, await stdout);
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
