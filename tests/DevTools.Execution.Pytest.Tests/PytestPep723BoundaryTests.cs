using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

[TestClass]

public sealed class PytestPep723BoundaryTests
{
    [TestMethod]
    public void IpyHandler_DoesNotTakeDependencyService()
    {
        var ctor = typeof(IpyTestRequestHandler).GetConstructors().Single();
        Assert.IsFalse(ctor.GetParameters().Any(p => p.ParameterType == typeof(PytestDependencyService)));
    }

    [TestMethod]
    public void CPythonHandler_TakesDependencyService()
    {
        var ctor = typeof(PytestRequestHandler).GetConstructors().Single();
        Assert.IsTrue(ctor.GetParameters().Any(p => p.ParameterType == typeof(PytestDependencyService)));
    }

    [TestMethod]
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

        Assert.AreEqual(0, summary.Passed);
        Assert.AreEqual(1, summary.Failed);
        Assert.AreEqual(2, summary.Errors);
        Assert.AreEqual(0, summary.Skipped);
    }

    [TestMethod]
    public void FileFromNodeid_UsesSharedSeparatorConstants()
    {
        Assert.IsFalse(string.IsNullOrEmpty(IpyTestPath.NodeidSeparator));
        Assert.AreEqual("tests/a.py", IpyTestPath.FileFromNodeid("tests/a.py"));
        Assert.AreEqual("tests/a.py", IpyTestPath.FileFromNodeid("tests/a.py" + IpyTestPath.NodeidSeparator + "T::test_x"));
    }
}
