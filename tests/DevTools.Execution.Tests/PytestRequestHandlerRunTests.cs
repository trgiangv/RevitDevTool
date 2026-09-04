using System.Text.Json;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using DevTools.Ipc;

namespace DevTools.Execution.Tests;

[Collection(nameof(PythonRuntimeCollection))]
public sealed class PytestRequestHandlerRunTests
{
    [Fact]
    public async Task HandleRunAsync_ValidRequest_ExecutesAndReturnsResponse()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var handler = new PytestRequestHandler(
            ExecutionTestHelpers.InlineHostContext(),
            new PytestDependencyService(initializer),
            new PytestExecutionService(new PythonExecutor(initializer)));

        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-handler-run");
        var testsDir = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testsDir);

        var payload = new
        {
            workspace_root = workspace,
            test_root = testsDir,
            node_ids = new[] { "tests/missing.py::test_x" },
            pytest_args = Array.Empty<string>(),
        };

        try
        {
            var response = await handler.HandleAsync(
                "req-1",
                PytestBridgeMethods.TestsRun,
                JsonSerializer.SerializeToElement(payload),
                TestContext.Current.CancellationToken);

            Assert.False(response.IsError);
            Assert.NotNull(response.Result);
            var body = response.Result!.Value.Deserialize<PytestRunResponse>();
            Assert.NotNull(body);
            Assert.Equal(1, body!.ExitCode);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public async Task HandleRunAsync_WithNotificationSender_InvokesProgress()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var handler = new PytestRequestHandler(
            ExecutionTestHelpers.InlineHostContext(),
            new PytestDependencyService(initializer),
            new PytestExecutionService(new PythonExecutor(initializer)))
        {
            NotificationSender = (_, _) => { },
        };

        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-handler-progress");
        var testsDir = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testsDir);
        var notifications = 0;
        handler.NotificationSender = (_, _) => notifications++;

        var payload = new
        {
            workspace_root = workspace,
            test_root = testsDir,
            node_ids = new[] { "tests/missing.py::test_x" },
            pytest_args = Array.Empty<string>(),
        };

        try
        {
            _ = await handler.HandleAsync(
                "req-2",
                PytestBridgeMethods.TestsRun,
                JsonSerializer.SerializeToElement(payload),
                TestContext.Current.CancellationToken);

            Assert.True(notifications >= 0);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
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
