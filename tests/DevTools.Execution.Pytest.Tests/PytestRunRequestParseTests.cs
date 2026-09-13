using System.Text.Json;
using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

[TestClass]

public sealed class PytestRunRequestParseTests
{
    [TestMethod]
    public void TryParseRunRequest_NullParams_ReturnsError()
    {
        Assert.IsFalse(PytestExecutionService.TryParseRunRequest(null, out var request, out var error));
        Assert.IsNull(request);
        Assert.AreEqual("Pytest run request is required.", error);
    }

    [TestMethod]
    public void TryParseRunRequest_MissingTestRoot_ReturnsError()
    {
        var json = JsonSerializer.SerializeToElement(new { workspace_root = "C:\\ws", nodeids = new[] { "tests/a.py::test_x" } });

        Assert.IsFalse(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.AreEqual("test_root is required.", error);
    }

    [TestMethod]
    public void TryParseRunRequest_EmptyNodeIds_ReturnsError()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            workspace_root = "C:\\ws",
            test_root = "C:\\ws\\tests",
            nodeids = Array.Empty<string>(),
        });

        Assert.IsFalse(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.AreEqual("At least one nodeid is required.", error);
    }

    [TestMethod]
    public void TryParseRunRequest_ValidRequest_ResolvesWorkspaceAndTestRoot()
    {
        var workspace = CreateTempWorkspace();
        try
        {
            var testRoot = Path.Combine(workspace, "tests");
            Directory.CreateDirectory(testRoot);

            var json = JsonSerializer.SerializeToElement(new
            {
                workspace_root = workspace,
                test_root = "tests",
                nodeids = new[] { "tests/test_foo.py::test_bar" },
                pytest_args = new[] { "-v" },
            });

            Assert.IsTrue(PytestExecutionService.TryParseRunRequest(json, out var request, out var error));
            Assert.IsNull(error);
            Assert.IsNotNull(request);
            Assert.AreEqual(Path.GetFullPath(workspace), request.WorkspaceRoot);
            Assert.AreEqual(Path.GetFullPath(testRoot), request.TestRoot);
            Assert.ContainsSingle(request.NodeIds);
            Assert.AreEqual("tests/test_foo.py::test_bar", request.NodeIds[0]);
            Assert.ContainsSingle(request.PytestArgs);
            Assert.AreEqual("-v", request.PytestArgs[0]);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public void TryParseRunRequest_InvalidJson_ReturnsError()
    {
        var json = JsonDocument.Parse("""{"test_root":"x","nodeids":"not-an-array"}""").RootElement;

        Assert.IsFalse(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.StartsWith("Invalid pytest run request:", error, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TryParseRunRequest_FiltersBlankNodeIdsAndPytestArgs()
    {
        var workspace = CreateTempWorkspace();
        try
        {
            var testRoot = Path.Combine(workspace, "tests");
            Directory.CreateDirectory(testRoot);

            var json = JsonSerializer.SerializeToElement(new
            {
                workspace_root = workspace,
                test_root = "tests",
                nodeids = new[] { " ", "tests/test_foo.py::test_bar", "" },
                pytest_args = new[] { "", "-q", "  " },
            });

            Assert.IsTrue(PytestExecutionService.TryParseRunRequest(json, out var request, out var error));
            Assert.IsNull(error);
            Assert.IsNotNull(request);
            Assert.ContainsSingle(request.NodeIds);
            Assert.AreEqual("tests/test_foo.py::test_bar", request.NodeIds[0]);
            Assert.ContainsSingle(request.PytestArgs);
            Assert.AreEqual("-q", request.PytestArgs[0]);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public void TryParseRunRequest_AllBlankNodeIds_ReturnsError()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            workspace_root = "C:\\ws",
            test_root = "tests",
            nodeids = new[] { " ", "" },
        });

        Assert.IsFalse(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.AreEqual("At least one nodeid is required.", error);
    }

    [TestMethod]
    public void IpyTryParseRunRequest_DelegatesToPytestParser()
    {
        Assert.IsFalse(IpyTestExecutionService.TryParseRunRequest(null, out _, out var error));
        Assert.AreEqual("Pytest run request is required.", error);
    }

    [TestMethod]
    public void Error_IncludesPhaseInCollectionMessage()
    {
        var response = PytestExecutionService.Error("prepare", "Invalid pytest run request.", "detail");

        Assert.AreEqual(1, response.ExitCode);
        Assert.ContainsSingle(response.CollectionErrors);
        Assert.AreEqual("[prepare] Invalid pytest run request.", response.CollectionErrors[0].Message);
        Assert.AreEqual("detail", response.CollectionErrors[0].Traceback);
        Assert.AreEqual(0, response.Summary.Passed);
        Assert.AreEqual(1, response.Summary.Errors);
    }

    private static string CreateTempWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "pytest-parse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
