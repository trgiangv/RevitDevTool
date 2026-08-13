using DevTools.NUnit.Core.Contracts;
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
                "<filter><cat>Smoke</cat></filter>",
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
        Assert.Equal("<filter><cat>Smoke</cat></filter>", command.Filter);
        Assert.True(command.HostLaunch);
        Assert.Equal(90, command.HostTimeoutSeconds);
        Assert.Equal(120, command.HostLaunchTimeoutSeconds);
    }

    [Fact]
    public void TryParse_run_composes_name_into_nunit_xml()
    {
        var ok = RunnerCommandParser.TryParse(
            [
                "run",
                @"C:\tests\Sample.dll",
                "--host",
                "Revit",
                "--version",
                "2026",
                "--name",
                "Arithmetic_runs_inside_host"
            ],
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(
            "<filter><name>Arithmetic_runs_inside_host</name></filter>",
            command!.Filter);
    }

    [Fact]
    public void TryParse_run_composes_test_fullname_into_nunit_xml()
    {
        var ok = RunnerCommandParser.TryParse(
            [
                "run",
                @"C:\tests\Sample.dll",
                "--host",
                "Revit",
                "--version",
                "2026",
                "--test",
                "HostSmokeTests.Arithmetic_runs_inside_host"
            ],
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(
            "<filter><test>HostSmokeTests.Arithmetic_runs_inside_host</test></filter>",
            command!.Filter);
    }

    [Fact]
    public void TryParse_rejects_name_mixed_with_filter_xml()
    {
        var ok = RunnerCommandParser.TryParse(
            [
                "run",
                @"C:\tests\Sample.dll",
                "--host",
                "Revit",
                "--version",
                "2026",
                "--name",
                "Arithmetic_runs_inside_host",
                "--filter",
                "<filter><cat>Smoke</cat></filter>"
            ],
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal(DevTools.NUnit.Runner.Services.NUnitRunnerFilter.MixedFilterMessage, error);
    }

    [Fact]
    public void TryParse_rejects_unknown_debug_option()
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
        Assert.Equal("Unknown option '--debug'.", error);
    }

    [Fact]
    public void TryParse_accepts_arguments_built_by_shared_cli_contract()
    {
        var args = NUnitRunnerCli.BuildArguments(
            NUnitRunnerCli.RunCommand,
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            hostLaunch: true,
            names: ["Arithmetic_runs_inside_host"]).ToArray();

        var ok = RunnerCommandParser.TryParse(args, out var command, out var error);

        Assert.True(ok, error);
        Assert.Equal(NUnitRunnerCli.RunCommand, command!.Command);
        Assert.Equal("Revit", command.Host);
        Assert.Equal("2026", command.Version);
        Assert.True(command.HostLaunch);
        Assert.Equal(60, command.HostTimeoutSeconds);
        Assert.Equal(180, command.HostLaunchTimeoutSeconds);
        Assert.Equal(
            "<filter><name>Arithmetic_runs_inside_host</name></filter>",
            command.Filter);
    }
}
