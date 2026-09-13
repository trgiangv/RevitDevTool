using System.Reflection;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class ToolsetInvocationServicesTests
{
    private static readonly Type ServicesType =
        typeof(DotnetMethodResolver).Assembly.GetType("DevTools.Mcp.Catalog.Discovery.ToolsetInvocationServices", throwOnError: true)!;

    [TestMethod]
    public void GetService_ResolvesAugmentedContracts()
    {
        var request = DotnetToolsetTestHarness.CreateRequest(progressToken: new ProgressToken("p1"));
        var services = CreateServices(request);

        Assert.AreSame(request, GetService(services, typeof(RequestContext<CallToolRequestParams>)));
        Assert.AreSame(request.Server, GetService(services, typeof(McpServer)));
        Assert.AreEqual("ToolsetProgressReporter", GetService(services, typeof(IProgress<ProgressNotificationValue>))!.GetType().Name);
        Assert.IsTrue((bool)Invoke(services, "IsService", typeof(McpServer))!);
        Assert.IsTrue((bool)Invoke(services, "IsKeyedService", typeof(McpServer), null)!);
    }

    [TestMethod]
    public void GetKeyedService_FallsBackToInnerProvider()
    {
        var request = DotnetToolsetTestHarness.CreateRequest();
        var inner = new ServiceCollection();
        inner.AddKeyedSingleton<string>("probe", (_, _) => "value");
        request.Services = inner.BuildServiceProvider();
        var services = CreateServices(request);

        Assert.AreEqual("value", Invoke(services, "GetKeyedService", typeof(string), "probe"));
        Assert.AreEqual("value", Invoke(services, "GetRequiredKeyedService", typeof(string), "probe"));
    }

    [TestMethod]
    public void GetRequiredKeyedService_Throws_WhenMissing()
    {
        var services = CreateServices(DotnetToolsetTestHarness.CreateRequest());

        Assert.ThrowsExactly<TargetInvocationException>(() =>
            Invoke(services, "GetRequiredKeyedService", typeof(string), "missing"));
    }

    [TestMethod]
    public void ProgressReporter_ReportsWithoutThrowing()
    {
        var request = DotnetToolsetTestHarness.CreateRequest(progressToken: new ProgressToken("p1"));
        var services = CreateServices(request);
        var progress = (IProgress<ProgressNotificationValue>)GetService(services, typeof(IProgress<ProgressNotificationValue>))!;

        Exception? exception = null;
        try
        {
            progress.Report(new ProgressNotificationValue { Progress = 0.5f, Total = 1f });
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.IsNull(exception);
    }

    private static object CreateServices(RequestContext<CallToolRequestParams> request) =>
        ServicesType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, [typeof(RequestContext<CallToolRequestParams>)])!
            .Invoke([request]);

    private static object? GetService(object services, Type serviceType) =>
        Invoke(services, "GetService", serviceType);

    private static object? Invoke(object target, string methodName, params object?[] args) =>
        ServicesType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!
            .Invoke(target, args);
}
