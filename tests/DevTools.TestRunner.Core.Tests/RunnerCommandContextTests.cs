using DevTools.TestRunner.Core.Parsing;

namespace DevTools.TestRunner.Core.Tests;

public sealed class RunnerCommandContextTests
{
    [Fact]
    public void TryCreate_normalizes_host_debug_and_framework()
    {
        var created = RunnerCommandContext.TryCreate(
            @"C:\tests\Sample.dll",
            " Revit ",
            " 2026 ",
            true,
            60,
            180,
            false,
            42,
            " EXAMPLE ",
            out var context,
            out var error);

        Assert.True(created, error);
        Assert.Equal("example", context!.FrameworkId);
        Assert.Equal("Revit", context.HostName);
        Assert.Equal("2026", context.HostVersion);
        Assert.True(context.ForceLaunch);
        Assert.True(context.Debug);
        Assert.Equal(42, context.DebugParentPid);
    }

    [Fact]
    public void TryCreate_requires_framework_id()
    {
        var created = RunnerCommandContext.TryCreate(
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            false,
            60,
            180,
            false,
            null,
            framework: null,
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("--framework is required.", error);
    }

    [Fact]
    public void TryCreate_forwards_an_unknown_framework_id()
    {
        var created = RunnerCommandContext.TryCreate(
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            false,
            60,
            180,
            false,
            null,
            "xunit",
            out var context,
            out var error);

        Assert.True(created, error);
        Assert.Equal("xunit", context!.FrameworkId);
    }

    [Fact]
    public void NormalizeFrameworkId_rejects_whitespace()
    {
        Assert.Throws<ArgumentException>(() => RunnerCommandContext.NormalizeFrameworkId(" "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_requires_assembly_path(string assemblyPath)
    {
        var created = RunnerCommandContext.TryCreate(
            assemblyPath,
            "Revit",
            "2026",
            false,
            60,
            180,
            false,
            null,
            "nunit",
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("Assembly path is required.", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_requires_host_name(string hostName)
    {
        var created = RunnerCommandContext.TryCreate(
            @"C:\tests\Sample.dll",
            hostName,
            "2026",
            false,
            60,
            180,
            false,
            null,
            "nunit",
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("--host is required.", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_requires_host_version(string hostVersion)
    {
        var created = RunnerCommandContext.TryCreate(
            @"C:\tests\Sample.dll",
            "Revit",
            hostVersion,
            false,
            60,
            180,
            false,
            null,
            "nunit",
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("--host-version is required.", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryCreate_rejects_non_positive_debug_parent_pid(int debugParentPid)
    {
        var created = RunnerCommandContext.TryCreate(
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            false,
            60,
            180,
            false,
            debugParentPid,
            "nunit",
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("--debug-parent-pid requires a positive process id.", error);
    }
}
