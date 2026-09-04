using System.Reflection;
using System.Text.Json;
using DevTools.Execution.External.Testing;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using Python.Runtime;

namespace DevTools.Execution.Tests;

public sealed class PytestExecutionServiceTests
{
    public PytestExecutionServiceTests()
    {
        PythonEmbedded.Configure(HostApp.Revit);
    }

    [Fact]
    public void ResolveRootFolder_UsesDirectory_WhenTestRootExists()
    {
        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-root-folder");
        var testRoot = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testRoot);
        try
        {
            var request = new PytestRunRequest(workspace, testRoot, ["tests/a.py::test_x"], []);
            var resolved = InvokeResolveRootFolder(request);
            Assert.Equal(Path.GetFullPath(testRoot), resolved);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public void ResolveAnchorFile_UsesInitPy_WhenAnchorMissing()
    {
        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-anchor");
        var testRoot = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testRoot);
        var initFile = Path.Combine(testRoot, "__init__.py");
        File.WriteAllText(initFile, string.Empty);
        try
        {
            var anchor = InvokeResolveAnchorFile(Path.Combine(testRoot, "missing.py"), testRoot);
            Assert.Equal(initFile, anchor);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public void ResolveAnchorFile_FallsBackToRootFolder_WhenNoFileExists()
    {
        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-anchor-root");
        var testRoot = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testRoot);
        try
        {
            var anchor = InvokeResolveAnchorFile(Path.Combine(testRoot, "missing.py"), testRoot);
            Assert.Equal(testRoot, anchor);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Collection(nameof(PythonRuntimeCollection))]
    public sealed class RunTests
    {
        [Fact]
        public async Task Run_ExecutesRunnerAndReturnsCollectionError_ForMissingNode()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var executor = new PythonExecutor(initializer);
            var service = new PytestExecutionService(executor);

            var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-run-missing");
            var testsDir = Path.Combine(workspace, "tests");
            Directory.CreateDirectory(testsDir);

            try
            {
                var request = new PytestRunRequest(
                    workspace,
                    testsDir,
                    ["tests/missing.py::test_x"],
                    []);

                var response = service.Run(request);

                Assert.Equal(1, response.ExitCode);
                Assert.NotEmpty(response.CollectionErrors);
            }
            finally
            {
                TryDeleteDirectory(workspace);
            }
        }

        [Fact]
        public async Task Run_InvokesProgressCallback_WhenProvided()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var executor = new PythonExecutor(initializer);
            var service = new PytestExecutionService(executor);

            var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-run-progress");
            var testsDir = Path.Combine(workspace, "tests");
            Directory.CreateDirectory(testsDir);
            var progressMessages = new List<string>();

            try
            {
                var request = new PytestRunRequest(
                    workspace,
                    testsDir,
                    ["tests/missing.py::test_x"],
                    []);

                _ = service.Run(request, message => progressMessages.Add(message));

                Assert.NotNull(progressMessages);
            }
            finally
            {
                TryDeleteDirectory(workspace);
            }
        }

        [Fact]
        public async Task Run_WithExecutorScope_WritesResultJson()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var responseJson = JsonSerializer.Serialize(new PytestRunResponse(
                0,
                new PytestSummary(1, 0, 0, 0, 0, 0),
                [new PytestCaseResult("tests/a.py::test_ok", "passed", "call", 1, "", "", "", "")],
                [],
                "pytest"));

            var result = new PythonExecutor(initializer).Execute(
                Path.Combine(ExecutionTestHelpers.CreateTempDirectory("pytest-stub"), "anchor.py"),
                null,
                scope =>
                {
                    scope.Set(PythonInstances.ResultJson, new PyString(responseJson));
                    return JsonSerializer.Deserialize<PytestRunResponse>(responseJson)!;
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, result.Summary.Passed);
        }
    }

    private static string InvokeResolveRootFolder(PytestRunRequest request)
    {
        var method = typeof(PytestExecutionService).GetMethod(
            "ResolveRootFolder",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method.Invoke(null, [request])!;
    }

    private static string InvokeResolveAnchorFile(string anchorPath, string rootFolder)
    {
        var method = typeof(PytestExecutionService).GetMethod(
            "ResolveAnchorFile",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method.Invoke(null, [anchorPath, rootFolder])!;
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
