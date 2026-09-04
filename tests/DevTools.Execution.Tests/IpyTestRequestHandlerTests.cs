using System.Text.Json;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;
using DevTools.Ipc;

namespace DevTools.Execution.Tests;

public sealed class IpyTestRequestHandlerTests
{
    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        var handler = new IpyTestRequestHandler(null!);

        var response = await handler.HandleAsync("1", "tests/run", null, TestContext.Current.CancellationToken);

        Assert.True(response.IsError);
        Assert.Equal(IpcErrorCodes.MethodNotFound, response.ErrorDetail?.Code);
        Assert.Contains("Unknown method", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidParams_ReturnsPreparePhaseErrorResponse()
    {
        var handler = new IpyTestRequestHandler(null!);
        var json = JsonSerializer.SerializeToElement(new { workspace_root = "C:\\ws" });

        var response = await handler.HandleAsync("2", PytestBridgeMethods.IpyTestsRun, json, TestContext.Current.CancellationToken);

        Assert.False(response.IsError);
        Assert.NotNull(response.Result);
        var body = response.Result!.Value.Deserialize<PytestRunResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.ExitCode);
        Assert.Single(body.CollectionErrors);
        Assert.Contains("[prepare]", body.CollectionErrors[0].Message, StringComparison.Ordinal);
        Assert.Contains("test_root is required", body.CollectionErrors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportedMethods_UsesIpyTestsRun_not_tests_run()
    {
        var handler = new IpyTestRequestHandler(null!);

        Assert.Contains(PytestBridgeMethods.IpyTestsRun, handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(PytestBridgeMethods.TestsRun, handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
