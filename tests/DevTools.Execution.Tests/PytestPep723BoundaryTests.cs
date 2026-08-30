using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

public sealed class PytestPep723BoundaryTests
{
    [Fact]
    public void IpyHandler_DoesNotTakeDependencyService()
    {
        var ctor = typeof(IpyTestRequestHandler).GetConstructors().Single();
        Assert.DoesNotContain(
            ctor.GetParameters(),
            p => p.ParameterType == typeof(PytestDependencyService));
    }

    [Fact]
    public void CPythonHandler_TakesDependencyService()
    {
        var ctor = typeof(PytestRequestHandler).GetConstructors().Single();
        Assert.Contains(
            ctor.GetParameters(),
            p => p.ParameterType == typeof(PytestDependencyService));
    }

    [Fact]
    public void IpySummary_CountsCollectionErrorsOnceInErrorsNotFailed()
    {
        var results = new List<PytestCaseResult>
        {
            new("a.py::T::test_fail", "failed", "call", 1, "", "", "", ""),
            new("a.py::T::test_err", "error", "call", 1, "", "", "", ""),
        };
        var collectionErrors = new List<PytestCollectionError>
        {
            new("b.py", "b.py", "import failed", ""),
        };

        var summary = IpyTestExecutionService.BuildSummary(results, collectionErrors);

        Assert.Equal(0, summary.Passed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(2, summary.Errors);
        Assert.Equal(0, summary.Skipped);
    }

    [Fact]
    public void FileFromNodeid_UsesSharedSeparatorConstants()
    {
        Assert.Equal("::", IpyTestPath.NodeidSeparator);
        Assert.Equal("tests/a.py", IpyTestPath.FileFromNodeid("tests/a.py"));
        Assert.Equal("tests/a.py", IpyTestPath.FileFromNodeid("tests/a.py" + IpyTestPath.NodeidSeparator + "T::test_x"));
    }
}
