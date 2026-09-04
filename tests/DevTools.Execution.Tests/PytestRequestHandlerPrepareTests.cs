using System.Text.Json;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;
using DevTools.Execution.Providers.Python;
using DevTools.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class PytestRequestHandlerPrepareTests
{
    [Fact]
    public async Task HandleRunAsync_WhenPythonCannotInitialize_ReturnsPrepareError()
    {
        var initializer = ExecutionTestHelpers.CreatePythonInitializer(
            pip: new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance));
        var handler = new PytestRequestHandler(
            ExecutionTestHelpers.InlineHostContext(),
            new PytestDependencyService(initializer),
            new PytestExecutionService(new PythonExecutor(initializer)));

        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-prepare-fail");
        var testsDir = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testsDir);
        var testFile = Path.Combine(testsDir, "test_sample.py");
        await File.WriteAllTextAsync(testFile, "def test_x(): pass", TestContext.Current.CancellationToken);

        var payload = JsonSerializer.SerializeToElement(new
        {
            workspace_root = workspace,
            test_root = testsDir,
            node_ids = new[] { "tests/test_sample.py::test_x" },
            pytest_args = Array.Empty<string>(),
        });

        try
        {
            var response = await handler.HandleAsync(
                "prepare-fail",
                PytestBridgeMethods.TestsRun,
                payload,
                TestContext.Current.CancellationToken);

            Assert.False(response.IsError);
            var body = response.Result!.Value.Deserialize<PytestRunResponse>();
            Assert.NotNull(body);
            Assert.Equal(1, body!.ExitCode);
            Assert.Contains(body.CollectionErrors, error => error.Message.Contains("[prepare]", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }
}
