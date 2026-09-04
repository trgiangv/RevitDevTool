using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

public class IpyTestPathTests
{
    [Fact]
    public void FileFromNodeid_StripsTestIdentity()
    {
        Assert.Equal("tests/Revit/test_math_ipy.py", IpyTestPath.FileFromNodeid("tests/Revit/test_math_ipy.py::TestMath::test_add"));
        Assert.Equal("tests/Revit/test_math.py", IpyTestPath.FileFromNodeid("tests/Revit/test_math.py"));
    }

    [Fact]
    public void ToNodeidPrefix_UsesForwardSlashesRelativeToWorkspace()
    {
        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ipy-prefix-" + Guid.NewGuid().ToString("N")));
        var testsDir = Path.Combine(workspace, "tests", "Revit");
        Directory.CreateDirectory(testsDir);
        var testFile = Path.Combine(testsDir, "test_math_ipy.py");
        File.WriteAllText(testFile, "# stub");

        try
        {
            Assert.Equal("tests/Revit/test_math_ipy.py", IpyTestPath.ToNodeidPrefix(testFile, workspace));
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch
            {
                // best effort
            }
        }
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
