using System.Text.Json;
using DevTools.Execution.External.Handlers;
using DevTools.Hosting;
using DevTools.Ipc;

namespace DevTools.Execution.Tests;

public sealed class InstanceRequestHandlerTests
{
    [Fact]
    public async Task InstanceInfo_ReturnsHostVersionAndProcessId()
    {
        var hostInfo = new StubHostAppInfo(HostApp.Revit, "2025");
        var handler = new InstanceRequestHandler(hostInfo);

        var response = await handler.HandleAsync("42", IpcBridgeMethods.InstanceInfo, null, TestContext.Current.CancellationToken);

        Assert.False(response.IsError);
        Assert.NotNull(response.Result);
        var info = response.Result!.Value.Deserialize<InstanceInfo>();
        Assert.NotNull(info);
        Assert.Equal(hostInfo.Host.ToString(), info!.HostApp);
        Assert.Equal(hostInfo.VersionNumber, info.VersionNumber);
        Assert.Equal(Environment.ProcessId, info.ProcessId);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        var handler = new InstanceRequestHandler(new StubHostAppInfo(HostApp.AutoCad, "2026"));

        var response = await handler.HandleAsync("1", "instance/unknown", null, TestContext.Current.CancellationToken);

        Assert.True(response.IsError);
        Assert.Equal(IpcErrorCodes.MethodNotFound, response.ErrorDetail?.Code);
    }

    private sealed class StubHostAppInfo(HostApp host, string version) : IHostAppInfo
    {
        public HostApp Host { get; } = host;
        public string VersionNumber { get; } = version;
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }
}
