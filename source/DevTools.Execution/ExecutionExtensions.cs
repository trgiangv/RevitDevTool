using DevTools.Execution.External;
using DevTools.Execution.External.Connections;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Mcp;
using DevTools.Execution.External.Mcp.Dispatchers;
using DevTools.Execution.External.Mcp.Handlers;
using DevTools.Execution.External.Mcp.Registry;
using DevTools.Execution.External.Testing;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.Dotnet;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.McpParser.Models;
using DevTools.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DevTools.Execution;

/// <summary>
/// Shared execution registrations for <see cref="IServiceCollection"/> (same layering as host apps wiring inside <c>ConfigureServices</c>).
/// Host-specific adapters (<see cref="IHostContextExecutor"/>, <see cref="ICommandDiscovery"/>, …) belong in the add-in.
/// </summary>
public static class ExecutionExtensions
{
    /// <summary>
    /// Registers execution orchestration, script/assembly providers, MCP registry, and in-proc pipe server.
    /// From the add-in host: after registering bridges/adapters on <see cref="HostApplicationBuilder.Services"/>, call <c>services.AddExecutionServices()</c>.
    /// </summary>
    public static IServiceCollection AddExecutionServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ITelemetry, NoOpTelemetry>();

        services.AddKeyedSingleton<PyEnvironmentProvider, PixiEnvironmentProvider>(PythonBackend.Pixi);
        services.AddKeyedSingleton<PyEnvironmentProvider, PipEnvironmentProvider>(PythonBackend.Pip);
        services.AddSingleton<PythonInitializer>();
        services.AddSingleton<PythonExecutor>();

        services.AddSingleton<ITreeStateManager, TreeStateManager>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IExecutionOrchestrator, ExecutionOrchestrator>();
        services.AddSingleton<IPackageService, PackageService>();

        services.AddSingleton<IExecutionProvider, AssemblyExecutionProvider>();
        services.AddSingleton<IExecutionProvider, ScriptExecutionProvider>();
        services.AddKeyedSingleton<IExecutionProvider, AssemblyExecutionProvider>(ExecutionMode.Assembly);
        services.AddKeyedSingleton<IExecutionProvider, ScriptExecutionProvider>(ExecutionMode.Script);

        services.AddSingleton<ConnectionState>();
        services.AddSingleton<InstanceRequestHandler>();
        services.AddSingleton<RegistryRequestHandler>();
        services.AddSingleton<PytestDependencyService>();
        services.AddSingleton<PytestExecutionService>();
        services.AddSingleton<PytestRequestHandler>();

        services.AddSingleton<DotnetToolRegistryProvider>();
        services.AddSingleton<PythonToolRegistryProvider>();
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<DotnetToolRegistryProvider>());
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<PythonToolRegistryProvider>());
        services.AddSingleton<ToolRegistryCatalogLoader>();
        services.AddSingleton<ToolRegistryStore>();
        services.AddSingleton<ToolExecutionDispatcher>();
        services.AddSingleton<PromptExecutionDispatcher>();
        services.AddSingleton<ResourceExecutionDispatcher>();
        services.AddSingleton<DevToolsPipeServer>();
        services.AddHostedService(sp => sp.GetRequiredService<DevToolsPipeServer>());

        return services;
    }
}
