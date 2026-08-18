using DevTools.Execution;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Testing;
using DevTools.Ipc;
using DevTools.Hosting;
using DevTools.NUnit.Host;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using DevTools.Testing.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Execution.Tests;

public sealed class BridgeHandlerRegistrationTests
{
    [Fact]
    public void AddExecutionServices_and_AddNUnitHostServices_register_only_neutral_testing_methods()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostAppInfo, FakeHostAppInfo>();
        services.AddSingleton<IHostContextExecutor, NoOpHostContextExecutor>();
        services.AddExecutionServices();
        services.AddNUnitHostServices();
        services.AddGenericTestingHostServices();

        var implementationTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IBridgeRequestHandler))
            .Select(descriptor => descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType())
            .Where(type => type is not null)
            .Cast<Type>()
            .ToList();

        Assert.Contains(typeof(InstanceRequestHandler), implementationTypes);
        Assert.Contains(typeof(PytestRequestHandler), implementationTypes);
        Assert.Contains(typeof(MarshaledTestingRequestHandler), implementationTypes);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostTestFrameworkProvider)
                && descriptor.ImplementationType == typeof(NUnitHostTestFrameworkProvider));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TestingProviderRegistry));

        var methods = new InstanceRequestHandler(new FakeHostAppInfo()).SupportedMethods
            .Concat([PytestBridgeMethods.TestsRun])
            .Concat(new TestingRequestHandler(
                new TestingProviderRegistry([new NoOpTestingProvider()]),
                "Revit",
                "2025").SupportedMethods)
            .ToList();

        Assert.Contains(PytestBridgeMethods.TestsRun, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(TestingProtocol.Hello, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(TestingProtocol.Run, methods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(TestingProtocol.Cancel, methods, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("testing/discover", methods, StringComparer.OrdinalIgnoreCase);
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

    private sealed class NoOpTestingProvider : IHostTestFrameworkProvider
    {
        public string FrameworkId => "provider.example";

        public TestingRunResponse Run(
            TestingRunRequest request,
            ITestingEventSink eventSink,
            CancellationToken cancellationToken) =>
            new(
                request.RunId,
                FrameworkId,
                null,
                Array.Empty<TestingCaseResult>(),
                TestingCancellationState.None,
                null,
                null);

        public bool Cancel(Guid runId) => false;
    }
}
