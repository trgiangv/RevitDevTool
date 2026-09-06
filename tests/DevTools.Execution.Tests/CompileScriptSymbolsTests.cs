using DevTools.Execution.Providers;
using DevTools.Hosting;

namespace DevTools.Execution.Tests;

public sealed class CompileScriptSymbolsTests
{
    [Fact]
    public void For_NullHost_ReturnsTraceAndDebugOnly()
    {
        Assert.Equal(["TRACE", "DEBUG"], CompileScriptSymbols.For(null));
    }

    [Fact]
    public void For_Revit2022_DefinesExactYearAndMinimalOrGreater()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "2022"));

        Assert.Equal(
            ["TRACE", "DEBUG", "REVIT2022_OR_GREATER", "REVIT2022", "REVIT"],
            symbols);
    }

    [Fact]
    public void For_Revit2025_LaddersFromMinimalThroughCurrent()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "2025"));

        Assert.Equal(
        [
            "TRACE", "DEBUG",
            "REVIT2022_OR_GREATER", "REVIT2023_OR_GREATER", "REVIT2024_OR_GREATER", "REVIT2025_OR_GREATER",
            "REVIT2025", "REVIT",
        ], symbols);
        Assert.DoesNotContain("REVIT2026_OR_GREATER", symbols);
    }

    [Fact]
    public void For_AutoCad2025_UsesAutocadPrefix()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.AutoCad, "2025"));

        Assert.Contains("AUTOCAD2025_OR_GREATER", symbols);
        Assert.Contains("AUTOCAD2025", symbols);
        Assert.Contains("AUTOCAD", symbols);
        Assert.DoesNotContain("REVIT", symbols);
    }

    [Fact]
    public void For_Civil3d_UsesAutocadFamilySymbols()
    {
        var symbols = CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Civil3D, "2025"));

        Assert.Contains("AUTOCAD2025", symbols);
        Assert.Contains("AUTOCAD", symbols);
        Assert.DoesNotContain("CIVIL3D", symbols);
        Assert.DoesNotContain("CIVIL3D2025", symbols);
        Assert.DoesNotContain("REVIT", symbols);
    }

    [Fact]
    public void For_UnknownVersion_ReturnsBaseSymbols()
    {
        Assert.Equal(["TRACE", "DEBUG"], CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "Unknown")));
        Assert.Equal(["TRACE", "DEBUG"], CompileScriptSymbols.For(ExecutionTestHelpers.CreateHostAppInfo(HostApp.Navisworks, "2025")));
    }
}
