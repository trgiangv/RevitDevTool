using DevTools.Execution.External.Testing;

namespace DevTools.Mcp.Tests;

public class IpyTestPathTests
{
    [Fact]
    public void FileFromNodeid_StripsTestIdentity()
    {
        Assert.Equal("tests/Revit/test_math_ipy.py", IpyTestPath.FileFromNodeid("tests/Revit/test_math_ipy.py::TestMath::test_add"));
        Assert.Equal("tests/Revit/test_math.py", IpyTestPath.FileFromNodeid("tests/Revit/test_math.py"));
    }

    [Fact]
    public void GroupNodeIds_GroupsByResolvedFile_WithoutFilenameConvention()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ipy-ws"));
        var groups = IpyTestExecutionService.GroupNodeIds(
            [
                "tests/Revit/test_math_ipy.py::TestMath::test_add",
                "tests/Revit/test_math_ipy.py::TestMath::test_add_negative",
                "tests/Revit/test_active_state.py::test_active_view_info",
            ],
            root);

        Assert.Equal(2, groups.Count);
        var mathPath = Path.GetFullPath(Path.Combine(root, "tests", "Revit", "test_math_ipy.py"));
        Assert.True(groups.ContainsKey(mathPath));
        Assert.Equal(2, groups[mathPath].Count);
        var cpythonPath = Path.GetFullPath(Path.Combine(root, "tests", "Revit", "test_active_state.py"));
        Assert.True(groups.ContainsKey(cpythonPath));
    }
}
