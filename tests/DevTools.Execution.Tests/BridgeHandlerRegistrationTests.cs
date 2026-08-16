using DevTools.Execution;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;
using DevTools.Ipc;
using DevTools.Hosting;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Host;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Execution.Tests;

public sealed class BridgeHandlerRegistrationTests
{
    [Fact]
    public void AddExecutionServices_and_AddNUnitHostServices_register_distinct_bridge_methods()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostAppInfo, FakeHostAppInfo>();
        services.AddSingleton<IHostContextExecutor, NoOpHostContextExecutor>();
        services.AddExecutionServices();
        services.AddNUnitHostServices();

        var implementationTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IBridgeRequestHandler))
            .Select(descriptor => descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType())
            .Where(type => type is not null)
            .Cast<Type>()
            .ToList();

        Assert.Contains(typeof(InstanceRequestHandler), implementationTypes);
        Assert.Contains(typeof(PytestRequestHandler), implementationTypes);
        Assert.Contains(typeof(NUnitRequestHandler), implementationTypes);

        var methods = new InstanceRequestHandler(new FakeHostAppInfo()).SupportedMethods
            .Concat(new NUnitRequestHandler(
                new NoOpHostContextExecutor(),
                new NoOpNUnitHost(),
                new FakeHostAppInfo()).SupportedMethods)
            .Concat([PytestBridgeMethods.TestsRun])
            .ToList();

        Assert.Contains(PytestBridgeMethods.TestsRun, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(NUnitProtocol.Hello, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(NUnitProtocol.Discover, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(NUnitProtocol.Run, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(NUnitProtocol.Cancel, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(methods.Count, methods.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

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

    private sealed class NoOpNUnitHost : INUnitHost
    {
        public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request) =>
            new(Array.Empty<NUnitDiscoveredTest>());

        public NUnitRunResponse Run(
            NUnitRunRequest request,
            Action<NUnitProgressEvent> publish,
            CancellationToken cancellationToken = default) =>
            new(request.RunId, new NUnitRunSummary(0, 0, 0, 0, 0, 0), Array.Empty<NUnitCaseResult>());

        public void Cancel(Guid runId)
        {
        }
    }
}
