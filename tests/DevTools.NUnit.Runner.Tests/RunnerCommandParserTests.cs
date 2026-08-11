using DevTools.NUnit.Runner.Parsing;

namespace DevTools.NUnit.Runner.Tests;

public sealed class RunnerCommandParserTests
{
    [Fact]
    public void TryParse_discover_requires_host_and_version()
    {
        var ok = RunnerCommandParser.TryParse(
            ["discover", @"C:\tests\Sample.dll"],
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal("--host is required.", error);
    }

    [Fact]
    public void TryParse_run_parses_filter_and_host_options()
    {
        var ok = RunnerCommandParser.TryParse(
            [
                "run",
                @"C:\tests\Sample.dll",
                "--host",
                "Revit",
                "--version",
                "2025",
                "--filter",
                "cat==Smoke",
                "--host-launch",
                "--host-timeout",
                "90",
                "--host-launch-timeout",
                "120"
            ],
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.NotNull(command);
        Assert.Equal("run", command!.Command);
        Assert.Equal("Revit", command.Host);
        Assert.Equal("2025", command.Version);
        Assert.Equal("cat==Smoke", command.Filter);
        Assert.False(command.WaitForDebugger);
        Assert.True(command.HostLaunch);
        Assert.Equal(90, command.HostTimeoutSeconds);
        Assert.Equal(120, command.HostLaunchTimeoutSeconds);
    }

    [Fact]
    public void TryParse_rejects_debug_option()
    {
        var ok = RunnerCommandParser.TryParse(
            [
                "run",
                @"C:\tests\Sample.dll",
                "--host",
                "Revit",
                "--version",
                "2025",
                "--debug",
                "wait"
            ],
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal("Host-process debugging is not supported in this experimental release.", error);
    }
}
