using DevTools.NUnit.Runner.Commands;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Runner.Services;

namespace DevTools.NUnit.Runner.Tests;

public sealed class DiscoverCommandTests
{
    [Fact]
    public void SplitNameAndTestFilter_reads_composed_name_and_test_nodes()
    {
        var xml = NUnitRunnerFilter.Compose(["Arithmetic"], ["Fixture.Beta"], xml: null);

        DiscoverCommand.SplitNameAndTestFilter(xml, out var names, out var fullNames);

        Assert.Equal(["Arithmetic"], names);
        Assert.Equal(["Fixture.Beta"], fullNames);
    }

    [Fact]
    public void SplitNameAndTestFilter_empty_xml_is_unfiltered()
    {
        DiscoverCommand.SplitNameAndTestFilter(null, out var names, out var fullNames);

        Assert.Empty(names);
        Assert.Empty(fullNames);
    }

    [Fact]
    public async Task ExecuteAsync_missing_assembly_is_cli_error()
    {
        var ok = RunnerCommandLine.TryCreate(
            "discover",
            Path.Combine(Path.GetTempPath(), "missing-nunit-tests.dll"),
            "Revit",
            "2026",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: true,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out var options,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(RunnerExitCode.CliError, await DiscoverCommand.ExecuteAsync(options!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_reads_pe_without_a_host()
    {
        var ok = RunnerCommandLine.TryCreate(
            "discover",
            typeof(DiscoverCommandTests).Assembly.Location,
            "Revit",
            "2026",
            names: null,
            tests: null,
            filterXml: null,
            hostLaunch: true,
            hostTimeoutSeconds: 60,
            hostLaunchTimeoutSeconds: 180,
            debug: false,
            debugParentPid: null,
            out var options,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(RunnerExitCode.Ok, await DiscoverCommand.ExecuteAsync(options!, TestContext.Current.CancellationToken));
    }
}
