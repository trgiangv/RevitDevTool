using DevTools.Execution.External;
using DevTools.Execution.External.Connections;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Mcp.Dispatchers;
using DevTools.Execution.External.Mcp.Hosting;
using DevTools.Execution.External.Mcp.Registry;
using DevTools.Execution.External.Testing;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.Dotnet;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Mcp.Handlers;
using DevTools.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

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
    public static IServiceCollection AddExecutionServices(
        this IServiceCollection services,
        bool registerDefaultScriptProvider = true)
    {
        services.TryAddSingleton<ITelemetry, NoOpTelemetry>();

        services.AddKeyedSingleton<PyEnvironmentProvider, PixiEnvironmentProvider>(PythonBackend.Pixi);
        services.AddKeyedSingleton<PyEnvironmentProvider, PipEnvironmentProvider>(PythonBackend.Pip);
        services.AddSingleton<PythonInitializer>();
        services.AddSingleton<PythonExecutor>();

        services.AddSingleton<NugetManager>();
        services.AddSingleton<PackageVersionChecker>();
        services.AddSingleton<PixiPackageHelper>();
        services.AddSingleton<FSharpDependencyResolver>();
        services.AddSingleton<FSharpExecutor>();
        services.AddSingleton<PythonToolsetParser>();
        services.AddSingleton<DotnetMcpAssemblyParser>();
        services.AddSingleton<CSharpCompiler>();
        services.AddSingleton<CSharpCompilationCache>();
        services.AddSingleton<FSharpCompilationCache>();
        services.AddSingleton<ScriptExecutionStrategyFactory>();
        services.TryAddSingleton<IScriptExecutionStrategyFactory>(
            sp => sp.GetRequiredService<ScriptExecutionStrategyFactory>());

        services.AddSingleton<ITreeStateManager, TreeStateManager>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IExecutionOrchestrator, ExecutionOrchestrator>();
        services.AddSingleton<IPackageService, PackageService>();

        services.AddSingleton<IExecutionProvider, AssemblyExecutionProvider>();
        services.AddKeyedSingleton<IExecutionProvider, AssemblyExecutionProvider>(ContainerMode.Assembly);

        if (registerDefaultScriptProvider)
        {
            services.AddSingleton<IExecutionProvider, ScriptExecutionProvider>();
            services.AddKeyedSingleton<IExecutionProvider, ScriptExecutionProvider>(ContainerMode.Script);
        }

        services.AddSingleton<ConnectionState>();
        services.AddSingleton<IMcpExecutionTracker, ConnectionStateExecutionTracker>();
        services.AddSingleton<IBridgeRequestHandler, InstanceRequestHandler>();
        services.AddSingleton<IBridgeRequestHandler, McpBridgeRequestHandler>();
        services.AddSingleton<PytestDependencyService>();
        services.AddSingleton<PytestExecutionService>();
        services.AddSingleton<IBridgeRequestHandler, PytestRequestHandler>();

        services.AddSingleton<DotnetMcpRegistryProvider>();
        services.AddSingleton<PythonMcpRegistryProvider>();
        services.AddSingleton<BuiltInMcpRegistryProvider>();
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<DotnetMcpRegistryProvider>());
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<PythonMcpRegistryProvider>());
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<BuiltInMcpRegistryProvider>());
        services.AddSingleton<McpCatalogLoader>();
        services.AddSingleton<McpCatalogStore>();
        services.AddSingleton<IBuiltInMcpTool, CSharpCodeTool>();
        services.AddSingleton<IBuiltInMcpTool, PythonCodeTool>();
        services.AddSingleton<IBuiltInMcpTool>(sp =>
            new OpenDocumentTool(sp.GetService<IDocumentBridge>() ?? NullDocumentBridge.Instance));
        services.AddSingleton<McpToolsetContextManager>();
        services.AddSingleton<DotnetMethodResolver>();
        services.AddSingleton<McpPrimitiveDispatcher>();
        services.AddSingleton<IMcpPrimitiveDispatcher>(sp => sp.GetRequiredService<McpPrimitiveDispatcher>());
        services.AddSingleton<DevToolsPipeServer>();
        services.AddHostedService(sp => sp.GetRequiredService<DevToolsPipeServer>());
        services.AddSingleton<McpServerPrimitiveCollection<McpServerTool>>(_ => []);
        services.AddSingleton<McpServerPrimitiveCollection<McpServerPrompt>>(_ => []);
        services.AddSingleton<McpServerResourceCollection>(_ => []);
        services.AddSingleton<HostMcpServerOptionsFactory>();
        services.AddSingleton<HostMcpServerHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<HostMcpServerHostedService>());

        return services;
    }
}
