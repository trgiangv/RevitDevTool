using System.Reflection;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class ToolsetInvocationServicesTests
{
    private static readonly Type ServicesType =
        typeof(DotnetMethodResolver).Assembly.GetType("DevTools.Mcp.Catalog.Discovery.ToolsetInvocationServices", throwOnError: true)!;

    [Fact]
    public void GetService_ResolvesAugmentedContracts()
    {
        var request = DotnetToolsetTestHarness.CreateRequest(progressToken: new ProgressToken("p1"));
        var services = CreateServices(request);

        Assert.Same(request, GetService(services, typeof(RequestContext<CallToolRequestParams>)));
        Assert.Same(request.Server, GetService(services, typeof(McpServer)));
        Assert.Equal("ToolsetProgressReporter", GetService(services, typeof(IProgress<ProgressNotificationValue>))!.GetType().Name);
        Assert.True((bool)Invoke(services, "IsService", typeof(McpServer))!);
        Assert.True((bool)Invoke(services, "IsKeyedService", typeof(McpServer), null)!);
    }

    [Fact]
    public void GetKeyedService_FallsBackToInnerProvider()
    {
        var request = DotnetToolsetTestHarness.CreateRequest();
        var inner = new ServiceCollection();
        inner.AddKeyedSingleton<string>("probe", (_, _) => "value");
        request.Services = inner.BuildServiceProvider();
        var services = CreateServices(request);

        Assert.Equal("value", Invoke(services, "GetKeyedService", typeof(string), "probe"));
        Assert.Equal("value", Invoke(services, "GetRequiredKeyedService", typeof(string), "probe"));
    }

    [Fact]
    public void GetRequiredKeyedService_Throws_WhenMissing()
    {
        var services = CreateServices(DotnetToolsetTestHarness.CreateRequest());

        Assert.Throws<TargetInvocationException>(() =>
            Invoke(services, "GetRequiredKeyedService", typeof(string), "missing"));
    }

    [Fact]
    public void ProgressReporter_ReportsWithoutThrowing()
    {
        var request = DotnetToolsetTestHarness.CreateRequest(progressToken: new ProgressToken("p1"));
        var services = CreateServices(request);
        var progress = (IProgress<ProgressNotificationValue>)GetService(services, typeof(IProgress<ProgressNotificationValue>))!;

        var exception = Record.Exception(() => progress.Report(new ProgressNotificationValue { Progress = 0.5f, Total = 1f }));

        Assert.Null(exception);
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
