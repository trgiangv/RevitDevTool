using DevTools.Ipc;

namespace DevTools.Execution.Tests;

public class HostPipeNameTests
{
    [Fact]
    public void FormatAndParse_RoundTripsPytestAndMcpPipes()
    {
        var test = HostPipeName.FormatTest("Revit", "2025", 12345);
        var mcp = HostPipeName.FormatMcp("Revit", "2025", 12345);

        Assert.Equal("DevTools_Revit_2025_12345", test);
        Assert.Equal("DevToolsMcp_Revit_2025_12345", mcp);
        Assert.True(HostPipeName.IsTestPipe(test));
        Assert.False(HostPipeName.IsMcpPipe(test));
        Assert.True(HostPipeName.IsMcpPipe(mcp));
        Assert.False(HostPipeName.IsTestPipe(mcp));

        Assert.True(HostPipeName.TryParse(mcp, out var host, out var version, out var pid));
        Assert.Equal("Revit", host);
        Assert.Equal("2025", version);
        Assert.Equal(12345, pid);
        Assert.Equal(mcp, HostPipeName.ToMcpPipeName(test));
    }

    [Fact]
    public void TryParse_AcceptsSemverVersionSegments()
    {
        var pipe = HostPipeName.FormatMcp("Rhino", "8.0", 99);
        Assert.True(HostPipeName.TryParse(pipe, out var host, out var version, out var pid));
        Assert.Equal("Rhino", host);
        Assert.Equal("8.0", version);
        Assert.Equal(99, pid);
    }

    [Fact]
    public void TryParse_RejectsUnknownPrefixAndNonNumericPid()
    {
        Assert.False(HostPipeName.TryParse("Wrong_Revit_2025_12345", out _, out _, out _));
        Assert.False(HostPipeName.TryParse("DevTools_Revit_2025_notpid", out _, out _, out _));
        Assert.False(HostPipeName.TryParse("DevTools", out _, out _, out _));
    }

    [Fact]
    public void ExtractHost_ReturnsHostOrNull()
    {
        var pipe = HostPipeName.FormatTest("Civil3D", "2026", 42);
        Assert.Equal("Civil3D", HostPipeName.ExtractHost(pipe));
        Assert.Null(HostPipeName.ExtractHost("not-a-pipe"));
    }

    [Fact]
    public void ToMcpPipeName_ReturnsNull_ForInvalidPipe()
    {
        Assert.Null(HostPipeName.ToMcpPipeName("DevTools_BadPipe"));
    }

    [Fact]
    public void IsTestPipe_DistinguishesMcpPrefixFromTestPrefix()
    {
        var test = HostPipeName.FormatTest("Revit", "2025", 1);
        var mcp = HostPipeName.FormatMcp("Revit", "2025", 1);

        Assert.True(HostPipeName.IsTestPipe(test));
        Assert.False(HostPipeName.IsTestPipe(mcp));
        Assert.True(HostPipeName.IsMcpPipe(mcp));
    }
}
