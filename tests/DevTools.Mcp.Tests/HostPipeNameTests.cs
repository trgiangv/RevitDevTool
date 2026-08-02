namespace DevTools.Mcp.Tests;

public class HostPipeNameTests
{
    [Fact]
    public void FormatAndParse_RoundTripsPytestAndMcpPipes()
    {
        var pytest = HostPipeName.FormatPytest("Revit", "2025", 12345);
        var mcp = HostPipeName.FormatMcp("Revit", "2025", 12345);

        Assert.Equal("DevTools_Revit_2025_12345", pytest);
        Assert.Equal("DevToolsMcp_Revit_2025_12345", mcp);
        Assert.True(HostPipeName.IsPytestPipe(pytest));
        Assert.False(HostPipeName.IsMcpPipe(pytest));
        Assert.True(HostPipeName.IsMcpPipe(mcp));
        Assert.False(HostPipeName.IsPytestPipe(mcp));

        Assert.True(HostPipeName.TryParse(mcp, out var host, out var version, out var pid));
        Assert.Equal("Revit", host);
        Assert.Equal("2025", version);
        Assert.Equal(12345, pid);
        Assert.Equal(mcp, HostPipeName.ToMcpPipeName(pytest));
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
}
