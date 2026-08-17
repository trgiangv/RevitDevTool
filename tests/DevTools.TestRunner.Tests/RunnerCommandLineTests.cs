using DevTools.NUnit.Core.Contracts;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TestRunner.Parsing;

namespace DevTools.TestRunner.Tests;

public sealed class RunnerCommandLineTests
{
    [Fact]
    public void TryCreate_requires_host_and_host_version()
    {
        var ok = RunnerCommandLine.TryCreate(
            "discover",
            @"C:\tests\Sample.dll",
            host: "",
            hostVersion: "",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal("--host is required.", error);
    }

    [Fact]
    public void TryCreate_run_composes_name_into_nunit_xml()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            names: ["Arithmetic_runs_inside_host"],
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(
            "<filter><name>Arithmetic_runs_inside_host</name></filter>",
            command!.Filter);
        Assert.False(command.Debug);
        Assert.Null(command.DebugParentPid);
    }

    [Fact]
    public void TryCreate_run_composes_test_fullname_into_nunit_xml()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            names: null,
            tests: ["HostSmokeTests.Arithmetic_runs_inside_host"],
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(
            "<filter><test>HostSmokeTests.Arithmetic_runs_inside_host</test></filter>",
            command!.Filter);
    }

    [Fact]
    public void TryCreate_rejects_name_mixed_with_filter_xml()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            names: ["Arithmetic_runs_inside_host"],
            tests: null,
            filterXml: "<filter><cat>Smoke</cat></filter>",
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal(DevTools.TestRunner.Services.NUnitRunnerFilter.MixedFilterMessage, error);
    }

    [Fact]
    public void TryCreate_debug_parent_pid_implies_debug()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2025",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: 4242,
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.True(command!.Debug);
        Assert.Equal(4242, command.DebugParentPid);
    }

    [Fact]
    public void TryCreate_debug_flag_without_parent_pid_still_enables_attach()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2025",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: true,
            debugParentPid: null,
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.True(command!.Debug);
        Assert.Null(command.DebugParentPid);
    }

    [Fact]
    public void TryCreate_rejects_non_positive_debug_parent_pid()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2025",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: 0,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal("--debug-parent-pid requires a positive process id.", error);
    }

    [Fact]
    public void BuildArguments_uses_host_version_and_json_name_array()
    {
        var args = NUnitRunnerCli.BuildArguments(
            NUnitRunnerCli.RunCommand,
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            hostLaunch: true,
            names: ["Arithmetic_runs_inside_host"]);

        Assert.Equal(
            [
                "run",
                @"C:\tests\Sample.dll",
                "--host",
                "Revit",
                "--host-version",
                "2026",
                "--host-timeout",
                "60",
                "--host-launch-timeout",
                "180",
                "--host-launch",
                "--name",
                """["Arithmetic_runs_inside_host"]""",
            ],
            args);
    }

    [Fact]
    public void BuildArguments_debug_parent_pid_does_not_emit_debug_flag()
    {
        var args = NUnitRunnerCli.BuildArguments(
            NUnitRunnerCli.RunCommand,
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            hostLaunch: false,
            debugParentPid: 1001);

        Assert.DoesNotContain("--debug", args);
        Assert.Contains("--debug-parent-pid", args);
        Assert.Contains("1001", args);
        Assert.Contains("--host-version", args);
        Assert.DoesNotContain("--version", args);
    }

    [Fact]
    public void BuildArguments_preserves_comma_inside_fullname_via_json()
    {
        var args = NUnitRunnerCli.BuildArguments(
            NUnitRunnerCli.RunCommand,
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            hostLaunch: false,
            tests: ["Foo.Bar(Int32, String)"]);

        Assert.Equal("""["Foo.Bar(Int32, String)"]""", args[^1]);
    }

    [Fact]
    public void TryCreate_omitted_framework_defaults_to_nunit()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(TestingFrameworkIds.NUnit, command!.FrameworkId);
        Assert.False(command.UseGenericProtocol);
    }

    [Fact]
    public void TryCreate_explicit_framework_uses_generic_protocol()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out var command,
            out var error,
            framework: TestingFrameworkIds.NUnit);

        Assert.True(ok, error);
        Assert.Equal(TestingFrameworkIds.NUnit, command!.FrameworkId);
        Assert.True(command.UseGenericProtocol);
    }

    [Fact]
    public void TryCreate_normalizes_framework_case()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out var command,
            out var error,
            framework: "NUnit");

        Assert.True(ok, error);
        Assert.Equal(TestingFrameworkIds.NUnit, command!.FrameworkId);
        Assert.True(command.UseGenericProtocol);
    }

    [Fact]
    public void TryCreate_rejects_unknown_framework()
    {
        var ok = RunnerCommandLine.TryCreate(
            "run",
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: false,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out _,
            out var error,
            framework: "xunit");

        Assert.False(ok);
        Assert.Equal("Unsupported --framework 'xunit'.", error);
    }
}
