using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Host;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitReflectionRunnerTests
{
    private const string SpikeFixtureAssemblyName = "DevTools.NUnit.Host.Spike.Fixtures.dll";

    [Fact]
    public void Discover_finds_three_spike_tests()
    {
        var runner = CreateRunner();
        var response = runner.Discover(GetSpikeFixtureAssemblyPath(), filter: null);

        Assert.Equal(3, response.Cases.Count);
        Assert.Contains(response.Cases, test => test.Name == "Spike_Pass");
        Assert.Contains(response.Cases, test => test.Name == "Spike_Fail");
        Assert.Contains(response.Cases, test => test.Name == "Spike_Output");
    }

    [Fact]
    public void Discover_applies_name_filter()
    {
        var runner = CreateRunner();
        var response = runner.Discover(GetSpikeFixtureAssemblyPath(), filter: "Spike_Pass");

        Assert.Single(response.Cases);
        Assert.Equal("Spike_Pass", response.Cases[0].Name);
    }

    [Fact]
    public void Discover_applies_adapter_or_filter()
    {
        var runner = CreateRunner();
        var filter =
            "test == 'DevTools.NUnit.Host.Spike.Fixtures.SpikeFixtureTests.Spike_Pass' | " +
            "test == 'DevTools.NUnit.Host.Spike.Fixtures.SpikeFixtureTests.Spike_Output'";

        var response = runner.Discover(GetSpikeFixtureAssemblyPath(), filter);

        Assert.Equal(2, response.Cases.Count);
        Assert.Contains(response.Cases, test => test.Name == "Spike_Pass");
        Assert.Contains(response.Cases, test => test.Name == "Spike_Output");
    }

    [Fact]
    public void Run_reports_pass_fail_and_output()
    {
        var runner = CreateRunner();
        var published = new List<string>();
        var response = runner.Run(
            Guid.NewGuid(),
            GetSpikeFixtureAssemblyPath(),
            filter: null,
            progress => published.Add(progress.Case.Name));

        Assert.Equal(3, response.Cases.Count);
        Assert.Equal(2, response.Summary.Passed);
        Assert.Equal(1, response.Summary.Failed);
        Assert.Equal(3, published.Count);

        var fail = response.Cases.Single(test => test.Name == "Spike_Fail");
        Assert.Equal(NUnitOutcomes.Failed, fail.Outcome);

        var output = response.Cases.Single(test => test.Name == "Spike_Output");
        Assert.Equal(NUnitOutcomes.Passed, output.Outcome);
        Assert.Contains("spike-output-marker", output.Output ?? string.Empty, StringComparison.Ordinal);
    }

    private static NUnitReflectionRunner CreateRunner() =>
        new(new NUnitAssemblyLoader(), NullLogger<NUnitReflectionRunner>.Instance);

    private static string GetSpikeFixtureAssemblyPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SpikeFixtureAssemblyName);
        Assert.True(File.Exists(path), $"Spike fixture assembly not found at '{path}'.");
        return path;
    }
}
