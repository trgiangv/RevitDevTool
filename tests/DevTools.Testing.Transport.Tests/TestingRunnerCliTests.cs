using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class TestingRunnerCliTests
{
    static TestingRunRequest CreateRequest(TestingSelection selection) =>
        new(
            TestingProtocol.CurrentVersion,
            Guid.Empty,
            "provider.example",
            new TestingAssemblyReference(@"C:\tests\Sample.dll", null, null),
            selection,
            new Dictionary<string, string>());

    [Fact]
    public void BuildRunArguments_adds_force_launch_and_debug_parent_pid()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            CreateRequest(new TestingSelection([])),
            new TestingHostOptions("Revit", "2025", true, 60, 180, null, DebugParentPid: 4242));

        Assert.Contains(TestingRunnerCli.ForceLaunchOption, args);
        Assert.Contains(TestingRunnerCli.DebugParentPidOption, args);
        Assert.Equal("4242", args[args.IndexOf(TestingRunnerCli.DebugParentPidOption) + 1]);
    }

    [Fact]
    public void BuildRunArguments_omits_debug_parent_pid_when_not_positive()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            CreateRequest(new TestingSelection([])),
            new TestingHostOptions("Revit", "2025", false, 60, 180, null, DebugParentPid: 0));

        Assert.DoesNotContain(TestingRunnerCli.DebugParentPidOption, args);
    }

    [Fact]
    public void BuildRunArguments_trims_dedupes_and_serializes_names()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            CreateRequest(new TestingSelection([], Names: ["  Alpha  ", "alpha", "Beta"])),
            new TestingHostOptions("Revit", "2025", false, 60, 180, null));

        Assert.Contains(TestingRunnerCli.NameOption, args);
        Assert.Equal("""["Alpha","alpha","Beta"]""", args[args.IndexOf(TestingRunnerCli.NameOption) + 1]);
    }

    [Fact]
    public void BuildRunArguments_skips_blank_test_ids_and_names()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            CreateRequest(new TestingSelection(["  kept  ", " ", ""], Names: [" ", "kept-name"])),
            new TestingHostOptions("Revit", "2025", false, 60, 180, null));

        Assert.Contains(TestingRunnerCli.TestOption, args);
        Assert.Equal("""["kept"]""", args[args.IndexOf(TestingRunnerCli.TestOption) + 1]);
        Assert.Contains(TestingRunnerCli.NameOption, args);
        Assert.Equal("""["kept-name"]""", args[args.IndexOf(TestingRunnerCli.NameOption) + 1]);
    }

    [Fact]
    public void BuildRunArguments_trims_provider_payload_filter()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            CreateRequest(new TestingSelection([], ProviderPayload: "  <filter/>  ")),
            new TestingHostOptions("Revit", "2025", false, 60, 180, null));

        Assert.Contains(TestingRunnerCli.FilterOption, args);
        Assert.Equal("<filter/>", args[args.IndexOf(TestingRunnerCli.FilterOption) + 1]);
    }
}
