using System.Text.Json;
using DevTools.Execution.External.Handlers;
using DevTools.Hosting;
using DevTools.Ipc;

namespace DevTools.Execution.Tests;

[TestClass]

public sealed class InstanceRequestHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InstanceInfo_ReturnsHostVersionAndProcessId()
    {
        var hostInfo = new StubHostAppInfo(HostApp.Revit, "2025");
        var handler = new InstanceRequestHandler(hostInfo);

        var response = await handler.HandleAsync("42", IpcBridgeMethods.InstanceInfo, null, TestContext.CancellationToken);

        Assert.IsFalse(response.IsError);
        Assert.IsNotNull(response.Result);
        var info = response.Result!.Value.Deserialize<InstanceInfo>();
        Assert.IsNotNull(info);
        Assert.AreEqual(hostInfo.Host.ToString(), info!.HostApp);
        Assert.AreEqual(hostInfo.VersionNumber, info.VersionNumber);
        Assert.AreEqual(Environment.ProcessId, info.ProcessId);
    }

    [TestMethod]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        var handler = new InstanceRequestHandler(new StubHostAppInfo(HostApp.AutoCad, "2026"));

        var response = await handler.HandleAsync("1", "instance/unknown", null, TestContext.CancellationToken);

        Assert.IsTrue(response.IsError);
        Assert.AreEqual(IpcErrorCodes.MethodNotFound, response.ErrorDetail?.Code);
    }

    private sealed class StubHostAppInfo(HostApp host, string version) : IHostAppInfo
    {
        public HostApp Host { get; } = host;
        public string VersionNumber { get; } = version;
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }
}
