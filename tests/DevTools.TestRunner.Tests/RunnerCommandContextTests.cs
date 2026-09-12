using DevTools.TestRunner.Parsing;

namespace DevTools.TestRunner.Tests;

public sealed class RunnerCommandContextTests
{
    [Fact]
    public void TryCreate_accepts_a_valid_host_context()
    {
        var created = RunnerCommandContext.TryCreate(
            " Revit ",
            " 2026 ",
            true,
            60,
            180,
            false,
            42,
            requestTimeoutSeconds: 0,
            out var context,
            out var error);

        Assert.True(created, error);
        Assert.NotNull(context);
        Assert.Equal("Revit", context.HostName);
        Assert.Equal("2026", context.HostVersion);
        Assert.True(context.ForceLaunch);
        Assert.True(context.Debug);
        Assert.Equal(42, context.DebugParentPid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_requires_host_name(string hostName)
    {
        var created = RunnerCommandContext.TryCreate(
            hostName,
            "2026",
            false,
            60,
            180,
            false,
            null,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("Host name is required.", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_requires_host_version(string hostVersion)
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit",
            hostVersion,
            false,
            60,
            180,
            false,
            null,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("Host version is required.", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryCreate_rejects_non_positive_debug_parent_pid(int debugParentPid)
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit",
            "2026",
            false,
            60,
            180,
            false,
            debugParentPid,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("Debug parent pid requires a positive process id.", error);
    }

    [Fact]
    public void TryCreate_rejects_non_positive_per_test_timeout()
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit", "2026", false, 0, 180, false, null, 0, out _, out var error);

        Assert.False(created);
        Assert.Equal("Per-test timeout must be positive.", error);
    }

    [Fact]
    public void TryCreate_rejects_negative_request_timeout()
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit", "2026", false, 60, 180, false, null, -1, out _, out var error);

        Assert.False(created);
        Assert.Equal("Request timeout cannot be negative.", error);
    }
}
