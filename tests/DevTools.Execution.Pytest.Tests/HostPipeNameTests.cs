using DevTools.Ipc;

namespace DevTools.Execution.Tests;

[TestClass]

public class HostPipeNameTests
{
    [TestMethod]
    public void FormatAndParse_RoundTripsPytestAndMcpPipes()
    {
        var test = HostPipeName.FormatTest("Revit", "2025", 12345);
        var mcp = HostPipeName.FormatMcp("Revit", "2025", 12345);

        Assert.AreEqual("DevTools_Revit_2025_12345", test);
        Assert.AreEqual("DevToolsMcp_Revit_2025_12345", mcp);
        Assert.IsTrue(HostPipeName.IsTestPipe(test));
        Assert.IsFalse(HostPipeName.IsMcpPipe(test));
        Assert.IsTrue(HostPipeName.IsMcpPipe(mcp));
        Assert.IsFalse(HostPipeName.IsTestPipe(mcp));

        Assert.IsTrue(HostPipeName.TryParse(mcp, out var host, out var version, out var pid));
        Assert.AreEqual("Revit", host);
        Assert.AreEqual("2025", version);
        Assert.AreEqual(12345, pid);
        Assert.AreEqual(mcp, HostPipeName.ToMcpPipeName(test));
    }

    [TestMethod]
    public void TryParse_AcceptsSemverVersionSegments()
    {
        var pipe = HostPipeName.FormatMcp("Rhino", "8.0", 99);
        Assert.IsTrue(HostPipeName.TryParse(pipe, out var host, out var version, out var pid));
        Assert.AreEqual("Rhino", host);
        Assert.AreEqual("8.0", version);
        Assert.AreEqual(99, pid);
    }

    [TestMethod]
    public void TryParse_RejectsUnknownPrefixAndNonNumericPid()
    {
        Assert.IsFalse(HostPipeName.TryParse("Wrong_Revit_2025_12345", out _, out _, out _));
        Assert.IsFalse(HostPipeName.TryParse("DevTools_Revit_2025_notpid", out _, out _, out _));
        Assert.IsFalse(HostPipeName.TryParse("DevTools", out _, out _, out _));
    }

    [TestMethod]
    public void ExtractHost_ReturnsHostOrNull()
    {
        var pipe = HostPipeName.FormatTest("Civil3D", "2026", 42);
        Assert.AreEqual("Civil3D", HostPipeName.ExtractHost(pipe));
        Assert.IsNull(HostPipeName.ExtractHost("not-a-pipe"));
    }

    [TestMethod]
    public void ToMcpPipeName_ReturnsNull_ForInvalidPipe()
    {
        Assert.IsNull(HostPipeName.ToMcpPipeName("DevTools_BadPipe"));
    }

    [TestMethod]
    public void IsTestPipe_DistinguishesMcpPrefixFromTestPrefix()
    {
        var test = HostPipeName.FormatTest("Revit", "2025", 1);
        var mcp = HostPipeName.FormatMcp("Revit", "2025", 1);

        Assert.IsTrue(HostPipeName.IsTestPipe(test));
        Assert.IsFalse(HostPipeName.IsTestPipe(mcp));
        Assert.IsTrue(HostPipeName.IsMcpPipe(mcp));
    }
}
