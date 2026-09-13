using DevTools.Execution.Providers;
using DevTools.Hosting;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class CompileScriptSymbolsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void For_NullHost_ReturnsTraceAndDebugOnly()
    {
        Assert.AreSequenceEqual(["TRACE", "DEBUG"], CompileScriptSymbols.For(null));
    }

    [TestMethod]
    public void For_Revit2022_DefinesExactYearAndMinimalOrGreater()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "2022"));

        Assert.AreSequenceEqual(
            ["TRACE", "DEBUG", "REVIT2022_OR_GREATER", "REVIT2022", "REVIT"],
            symbols);
    }

    [TestMethod]
    public void For_Revit2025_LaddersFromMinimalThroughCurrent()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "2025"));

        Assert.AreSequenceEqual(
        [
            "TRACE", "DEBUG",
            "REVIT2022_OR_GREATER", "REVIT2023_OR_GREATER", "REVIT2024_OR_GREATER", "REVIT2025_OR_GREATER",
            "REVIT2025", "REVIT",
        ], symbols);
        Assert.DoesNotContain("REVIT2026_OR_GREATER", symbols);
    }

    [TestMethod]
    public void For_AutoCad2025_UsesAutocadPrefix()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.AutoCad, "2025"));

        Assert.Contains("AUTOCAD2025_OR_GREATER", symbols);
        Assert.Contains("AUTOCAD2025", symbols);
        Assert.Contains("AUTOCAD", symbols);
        Assert.DoesNotContain("REVIT", symbols);
    }

    [TestMethod]
    public void For_Civil3d_UsesAutocadFamilySymbols()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Civil3D, "2025"));

        Assert.Contains("AUTOCAD2025", symbols);
        Assert.Contains("AUTOCAD", symbols);
        Assert.DoesNotContain("CIVIL3D", symbols);
        Assert.DoesNotContain("CIVIL3D2025", symbols);
        Assert.DoesNotContain("REVIT", symbols);
    }

    [TestMethod]
    public void For_UnknownVersion_ReturnsBaseSymbols()
    {
        Assert.AreSequenceEqual(["TRACE", "DEBUG"], CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "Unknown")));
        Assert.AreSequenceEqual(["TRACE", "DEBUG"], CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Navisworks, "2025")));
    }
}
