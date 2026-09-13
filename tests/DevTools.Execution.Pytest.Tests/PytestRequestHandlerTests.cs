using System.Reflection;
using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;
using DevTools.Ipc;

namespace DevTools.Execution.Tests;

[TestClass]

public sealed class PytestRequestHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        var handler = CreateHandler();

        var response = await handler.HandleAsync("1", "tests/unknown", null, TestContext.CancellationToken);

        Assert.IsTrue(response.IsError);
        Assert.AreEqual(IpcErrorCodes.MethodNotFound, response.ErrorDetail?.Code);
        Assert.Contains("Unknown method", response.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task InvalidParams_ReturnsPreparePhaseErrorResponse()
    {
        var handler = CreateHandler();
        var json = JsonSerializer.SerializeToElement(new { workspace_root = "C:\\ws" });

        var response = await handler.HandleAsync("2", PytestBridgeMethods.TestsRun, json, TestContext.CancellationToken);

        Assert.IsFalse(response.IsError);
        Assert.IsNotNull(response.Result);
        var body = response.Result!.Value.Deserialize<PytestRunResponse>(JsonOptions);
        Assert.IsNotNull(body);
        Assert.AreEqual(1, body!.ExitCode);
        Assert.ContainsSingle(body.CollectionErrors);
        Assert.Contains("[prepare]", body.CollectionErrors[0].Message, StringComparison.Ordinal);
        Assert.Contains("test_root is required", body.CollectionErrors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ImplementsNotificationPublisher_for_batch_and_streaming_modes()
    {
        var handler = CreateHandler();
        Assert.IsInstanceOfType<IBridgeNotificationPublisher>(handler);
    }

    [TestMethod]
    public void NotificationSender_can_be_set_for_streaming_progress()
    {
        var handler = CreateHandler();
        var publisher = (IBridgeNotificationPublisher)handler;

        Assert.IsNull(publisher.NotificationSender);

        publisher.NotificationSender = (_, _) => { };
        Assert.IsNotNull(handler.NotificationSender);
        Assert.AreSame(publisher.NotificationSender, handler.NotificationSender);
    }

    [TestMethod]
    public void CreateProgressCallback_null_sender_batch_mode_does_not_require_progress()
    {
        var handler = CreateHandler();
        var method = typeof(PytestRequestHandler).GetMethod(
            "CreateProgressCallback",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        Assert.IsNull(method!.Invoke(handler, null));

        handler.NotificationSender = (_, _) => { };
        Assert.IsNotNull(method.Invoke(handler, null));
    }

    private static PytestRequestHandler CreateHandler() =>
        new(new NoOpHostContextExecutor(), null!, null!);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class NoOpHostContextExecutor : IHostContextExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default) =>
            Task.FromResult(handler());

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
