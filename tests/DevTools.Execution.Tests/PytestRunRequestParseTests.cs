using System.Text.Json;
using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

public sealed class PytestRunRequestParseTests
{
    [Fact]
    public void TryParseRunRequest_NullParams_ReturnsError()
    {
        Assert.False(PytestExecutionService.TryParseRunRequest(null, out var request, out var error));
        Assert.Null(request);
        Assert.Equal("Pytest run request is required.", error);
    }

    [Fact]
    public void TryParseRunRequest_MissingTestRoot_ReturnsError()
    {
        var json = JsonSerializer.SerializeToElement(new { workspace_root = "C:\\ws", nodeids = new[] { "tests/a.py::test_x" } });

        Assert.False(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.Equal("test_root is required.", error);
    }

    [Fact]
    public void TryParseRunRequest_EmptyNodeIds_ReturnsError()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            workspace_root = "C:\\ws",
            test_root = "C:\\ws\\tests",
            nodeids = Array.Empty<string>(),
        });

        Assert.False(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.Equal("At least one nodeid is required.", error);
    }

    [Fact]
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

            Assert.True(PytestExecutionService.TryParseRunRequest(json, out var request, out var error));
            Assert.Null(error);
            Assert.NotNull(request);
            Assert.Equal(Path.GetFullPath(workspace), request.WorkspaceRoot);
            Assert.Equal(Path.GetFullPath(testRoot), request.TestRoot);
            Assert.Single(request.NodeIds);
            Assert.Equal("tests/test_foo.py::test_bar", request.NodeIds[0]);
            Assert.Single(request.PytestArgs);
            Assert.Equal("-v", request.PytestArgs[0]);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public void TryParseRunRequest_InvalidJson_ReturnsError()
    {
        var json = JsonDocument.Parse("""{"test_root":"x","nodeids":"not-an-array"}""").RootElement;

        Assert.False(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.StartsWith("Invalid pytest run request:", error, StringComparison.Ordinal);
    }

    [Fact]
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

            Assert.True(PytestExecutionService.TryParseRunRequest(json, out var request, out var error));
            Assert.Null(error);
            Assert.NotNull(request);
            Assert.Single(request.NodeIds);
            Assert.Equal("tests/test_foo.py::test_bar", request.NodeIds[0]);
            Assert.Single(request.PytestArgs);
            Assert.Equal("-q", request.PytestArgs[0]);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public void TryParseRunRequest_AllBlankNodeIds_ReturnsError()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            workspace_root = "C:\\ws",
            test_root = "tests",
            nodeids = new[] { " ", "" },
        });

        Assert.False(PytestExecutionService.TryParseRunRequest(json, out _, out var error));
        Assert.Equal("At least one nodeid is required.", error);
    }

    [Fact]
    public void IpyTryParseRunRequest_DelegatesToPytestParser()
    {
        Assert.False(IpyTestExecutionService.TryParseRunRequest(null, out _, out var error));
        Assert.Equal("Pytest run request is required.", error);
    }

    [Fact]
    public void Error_IncludesPhaseInCollectionMessage()
    {
        var response = PytestExecutionService.Error("prepare", "Invalid pytest run request.", "detail");

        Assert.Equal(1, response.ExitCode);
        Assert.Single(response.CollectionErrors);
        Assert.Equal("[prepare] Invalid pytest run request.", response.CollectionErrors[0].Message);
        Assert.Equal("detail", response.CollectionErrors[0].Traceback);
        Assert.Equal(0, response.Summary.Passed);
        Assert.Equal(1, response.Summary.Errors);
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
