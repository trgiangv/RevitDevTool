using DevTools.Testing.Abstractions.Contracts;
using DevTools.TestRunner.Parsing;

namespace DevTools.TestRunner.Tests;

public sealed class RunnerCommandContextTests
{
    [Fact]
    public void TryCreate_accepts_a_defined_framework_id()
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
            TestFrameworkId.NUnit,
            requestTimeoutSeconds: 0,
            out var context,
            out var error);

        Assert.True(created, error);
        Assert.Equal(TestFrameworkId.NUnit, context!.FrameworkId);
        Assert.Equal("Revit", context.HostName);
        Assert.Equal("2026", context.HostVersion);
        Assert.True(context.ForceLaunch);
        Assert.True(context.Debug);
        Assert.Equal(42, context.DebugParentPid);
    }

    [Fact]
    public void TryCreate_rejects_an_undefined_framework_id()
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
            (TestFrameworkId)42,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("Framework id is required.", error);
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
            TestFrameworkId.NUnit,
            requestTimeoutSeconds: 0,
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
            TestFrameworkId.NUnit,
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
            @"C:\tests\Sample.dll",
            "Revit",
            hostVersion,
            false,
            60,
            180,
            false,
            null,
            TestFrameworkId.NUnit,
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
            @"C:\tests\Sample.dll",
            "Revit",
            "2026",
            false,
            60,
            180,
            false,
            debugParentPid,
            TestFrameworkId.NUnit,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.False(created);
        Assert.Equal("Debug parent pid requires a positive process id.", error);
    }
}
