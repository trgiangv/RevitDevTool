using System.Reflection;
using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Testing;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]

public sealed class IpyTestExecutionServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    public IpyTestExecutionServiceTests()
    {
        PythonEmbedded.Configure(HostApp.Revit);
    }

    [TestMethod]
    public void ParseMaxfail_ReturnsZero_WhenArgsMissingOrInvalid()
    {
        Assert.AreEqual(0, IpyTestExecutionService.ParseMaxfail(null));
        Assert.AreEqual(0, IpyTestExecutionService.ParseMaxfail(["-v"]));
        Assert.AreEqual(0, IpyTestExecutionService.ParseMaxfail(["--maxfail=0"]));
        Assert.AreEqual(0, IpyTestExecutionService.ParseMaxfail(["--maxfail=abc"]));
    }

    [TestMethod]
    public void ParseMaxfail_ParsesPositiveValue()
    {
        Assert.AreEqual(3, IpyTestExecutionService.ParseMaxfail(["--maxfail=3", "-v"]));
    }

    [TestMethod]
    public void BuildSummary_CountsPassedAndSkipped()
    {
        var results = new List<PytestCaseResult>
        {
            new("a.py::T::test_ok", "passed", "call", 1, "", "", "", ""),
            new("a.py::T::test_skip", "skipped", "call", 0, "", "", "", ""),
        };

        var summary = IpyTestExecutionService.BuildSummary(results, []);

        Assert.AreEqual(1, summary.Passed);
        Assert.AreEqual(0, summary.Failed);
        Assert.AreEqual(1, summary.Skipped);
        Assert.AreEqual(0, summary.Errors);
    }

    [TestMethod]
    public void Error_DelegatesToPytestExecutionService()
    {
        var response = IpyTestExecutionService.Error("run", "driver failed", "trace");

        Assert.AreEqual(1, response.ExitCode);
        Assert.ContainsSingle(response.CollectionErrors);
        Assert.AreEqual("[run] driver failed", response.CollectionErrors[0].Message);
        Assert.AreEqual("trace", response.CollectionErrors[0].Traceback);
    }

    [TestMethod]
    public async Task RunAsync_EmptyGroups_ReturnsPrepareError()
    {
        var workspace = CreateTempDirectory();
        try
        {
            var service = new IpyTestExecutionService(Mock.Of<IScriptExecutionStrategyFactory>());
            var request = new PytestRunRequest(workspace, workspace, ["   "], []);

            var response = await service.RunAsync(request, TestContext.CancellationToken);

            Assert.AreEqual(1, response.ExitCode);
            Assert.ContainsSingle(response.CollectionErrors);
            Assert.Contains("No IronPython test files", response.CollectionErrors[0].Message, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public async Task RunAsync_MissingTestFile_AddsCollectionError()
    {
        var workspace = CreateTempDirectory();
        try
        {
            var service = new IpyTestExecutionService(Mock.Of<IScriptExecutionStrategyFactory>());
            var missing = Path.Combine(workspace, "tests", "missing_ipy.py");
            var request = new PytestRunRequest(
                workspace,
                workspace,
                [$"tests/missing_ipy.py::TestCase::test_x"],
                []);

            var response = await service.RunAsync(request, TestContext.CancellationToken);

            Assert.AreEqual(1, response.ExitCode);
            Assert.IsEmpty(response.Results);
            var error = Assert.ContainsSingle(response.CollectionErrors);
            Assert.AreEqual(missing, error.Path);
            Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public async Task RunAsync_DriverWritesValidResult_ReturnsPassedSummary()
    {
        var workspace = CreateTempDirectory();
        var testFile = Path.Combine(workspace, "tests", "sample_ipy.py");
        Directory.CreateDirectory(Path.GetDirectoryName(testFile)!);
        File.WriteAllText(testFile, "# ipy stub");

        var strategy = new Mock<IExecutionStrategy>();
        strategy
            .Setup(s => s.ExecuteAsync(It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .Returns<IProgress<string>?, CancellationToken>((_, _) =>
            {
                WriteDriverResult("""
                    {
                      "engine": "ipy27",
                      "results": [
                        {
                          "nodeid": "tests/sample_ipy.py::TestCase::test_ok",
                          "outcome": "passed",
                          "phase": "call",
                          "duration_ms": 1,
                          "stdout": "",
                          "stderr": "",
                          "message": "",
                          "traceback": ""
                        }
                      ],
                      "collection_errors": []
                    }
                    """);
                return Task.FromResult(ExecutionResult.Succeeded());
            });

        var factory = new Mock<IScriptExecutionStrategyFactory>();
        factory
            .Setup(f => f.Create(ExecutionMode.IronPython, It.IsAny<string>(), workspace))
            .Returns(strategy.Object);

        try
        {
            var service = new IpyTestExecutionService(factory.Object);
            var request = new PytestRunRequest(
                workspace,
                workspace,
                ["tests/sample_ipy.py::TestCase::test_ok"],
                []);

            var response = await service.RunAsync(request, TestContext.CancellationToken);

            Assert.AreEqual(0, response.ExitCode);
            Assert.AreEqual("ipy27", response.Engine);
            Assert.AreEqual(1, response.Summary.Passed);
            Assert.IsEmpty(response.CollectionErrors);
            factory.Verify(f => f.Create(ExecutionMode.IronPython, It.IsAny<string>(), workspace), Times.Once);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public async Task RunAsync_DriverMissingResult_WhenExecutionFailed_UsesExecutionMessage()
    {
        var workspace = CreateTempDirectory();
        var testFile = Path.Combine(workspace, "tests", "fail_ipy.py");
        Directory.CreateDirectory(Path.GetDirectoryName(testFile)!);
        File.WriteAllText(testFile, "# ipy stub");

        var strategy = new Mock<IExecutionStrategy>();
        strategy
            .Setup(s => s.ExecuteAsync(It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(ExecutionResult.Failed("driver crashed")));

        var factory = new Mock<IScriptExecutionStrategyFactory>();
        factory
            .Setup(f => f.Create(ExecutionMode.IronPython, It.IsAny<string>(), workspace))
            .Returns(strategy.Object);

        try
        {
            var service = new IpyTestExecutionService(factory.Object);
            var request = new PytestRunRequest(
                workspace,
                workspace,
                ["tests/fail_ipy.py::TestCase::test_x"],
                []);

            var response = await service.RunAsync(request, TestContext.CancellationToken);

            Assert.AreEqual(1, response.ExitCode);
            Assert.IsEmpty(response.Results);
            var error = Assert.ContainsSingle(response.CollectionErrors);
            Assert.AreEqual("driver crashed", error.Message);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public async Task RunAsync_InvalidDriverJson_AddsCollectionError()
    {
        var workspace = CreateTempDirectory();
        var testFile = Path.Combine(workspace, "tests", "bad_json_ipy.py");
        Directory.CreateDirectory(Path.GetDirectoryName(testFile)!);
        File.WriteAllText(testFile, "# ipy stub");

        var strategy = new Mock<IExecutionStrategy>();
        strategy
            .Setup(s => s.ExecuteAsync(It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .Returns<IProgress<string>?, CancellationToken>((_, _) =>
            {
                WriteDriverResult("not-json");
                return Task.FromResult(ExecutionResult.Succeeded());
            });

        var factory = new Mock<IScriptExecutionStrategyFactory>();
        factory
            .Setup(f => f.Create(ExecutionMode.IronPython, It.IsAny<string>(), workspace))
            .Returns(strategy.Object);

        try
        {
            var service = new IpyTestExecutionService(factory.Object);
            var request = new PytestRunRequest(
                workspace,
                workspace,
                ["tests/bad_json_ipy.py::TestCase::test_x"],
                []);

            var response = await service.RunAsync(request, TestContext.CancellationToken);

            Assert.AreEqual(1, response.ExitCode);
            var error = Assert.ContainsSingle(response.CollectionErrors);
            Assert.AreEqual("Failed to read IronPython test JSON.", error.Message);
            Assert.Contains("not-json", error.Traceback, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public async Task RunAsync_Maxfail_StopsAfterFirstFailure()
    {
        var workspace = CreateTempDirectory();
        var first = Path.Combine(workspace, "tests", "first_ipy.py");
        var second = Path.Combine(workspace, "tests", "second_ipy.py");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        File.WriteAllText(first, "# first");
        File.WriteAllText(second, "# second");

        var createCount = 0;
        var strategy = new Mock<IExecutionStrategy>();
        strategy
            .Setup(s => s.ExecuteAsync(It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .Returns<IProgress<string>?, CancellationToken>((_, _) =>
            {
                WriteDriverResult("""
                    {
                      "engine": "ipy27",
                      "results": [
                        {
                          "nodeid": "tests/first_ipy.py::TestCase::test_fail",
                          "outcome": "failed",
                          "phase": "call",
                          "duration_ms": 1,
                          "stdout": "",
                          "stderr": "",
                          "message": "boom",
                          "traceback": ""
                        }
                      ],
                      "collection_errors": []
                    }
                    """);
                return Task.FromResult(ExecutionResult.Succeeded());
            });

        var factory = new Mock<IScriptExecutionStrategyFactory>();
        factory
            .Setup(f => f.Create(ExecutionMode.IronPython, It.IsAny<string>(), workspace))
            .Callback(() => createCount++)
            .Returns(strategy.Object);

        try
        {
            var service = new IpyTestExecutionService(factory.Object);
            var request = new PytestRunRequest(
                workspace,
                workspace,
                [
                    "tests/first_ipy.py::TestCase::test_fail",
                    "tests/second_ipy.py::TestCase::test_never_runs",
                ],
                ["--maxfail=1"]);

            var response = await service.RunAsync(request, TestContext.CancellationToken);

            Assert.AreEqual(1, response.ExitCode);
            Assert.AreEqual(1, response.Summary.Failed);
            Assert.AreEqual(1, createCount);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [TestMethod]
    public void ReadPayload_MapsNullDto_ToInvalidJsonError()
    {
        var tempResult = Path.Combine(Path.GetTempPath(), "ipy-read-payload-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(tempResult, "null");

        try
        {
            var payload = InvokeReadPayload(
                tempResult,
                @"C:\ws\tests\a.py",
                "tests/a.py",
                ExecutionResult.Succeeded());

            var errors = (IReadOnlyList<PytestCollectionError>)payload.GetType()
                .GetProperty("CollectionErrors")!
                .GetValue(payload)!;
            Assert.IsEmpty((IEnumerable<PytestCaseResult>)payload.GetType().GetProperty("Results")!.GetValue(payload)!);
            var error = Assert.ContainsSingle(errors);
            Assert.AreEqual("Invalid IronPython test JSON.", error.Message);
            Assert.AreEqual("null", error.Traceback);
        }
        finally
        {
            TryDeleteFile(tempResult);
        }
    }

    private static object InvokeReadPayload(
        string resultPath,
        string testPath,
        string prefix,
        ExecutionResult exec)
    {
        var method = typeof(IpyTestExecutionService).GetMethod(
            "ReadPayload",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        return method.Invoke(
            null,
            [resultPath, testPath, prefix, exec])!;
    }

    private static void WriteDriverResult(string json)
    {
        var driverDir = Path.GetDirectoryName(PythonEmbedded.IpyTestDriverScriptPath)
                        ?? throw new InvalidOperationException("Driver script directory is missing.");
        var requestPath = Path.Combine(driverDir, $"request_{Environment.ProcessId}.json");
        var request = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(requestPath));
        var resultPath = request.GetProperty("result_path").GetString()
                         ?? throw new InvalidOperationException("result_path is missing from driver request.");
        File.WriteAllText(resultPath, json);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ipy-exec-" + Guid.NewGuid().ToString("N"));
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

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
