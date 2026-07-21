using DevTools.Execution.External;
using DevTools.Execution.External.Connections;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Mcp.Registry;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.Dotnet;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Mcp.Hosting;
using DevTools.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Server;

namespace DevTools.Execution;

/// <summary>
/// Shared execution registrations for <see cref="IServiceCollection"/> (same layering as host apps wiring inside <c>ConfigureServices</c>).
/// Host-specific adapters (<see cref="IHostContextExecutor"/>, <see cref="ICommandDiscovery"/>, …) belong in the add-in.
/// Pytest host runner lives in <c>DevTools.Execution.Pytest</c> (<c>AddPytestHostRunner</c>).
/// </summary>
public static class ExecutionExtensions
{
    /// <summary>
    /// Registers execution core and in-host MCP server.
    /// Prefer calling <see cref="AddExecutionCore"/> and <see cref="AddInHostMcpServer"/> separately when composing selectively.
    /// Call <c>AddPytestHostRunner</c> from DevTools.Execution.Pytest when pytest MCP is required.
    /// </summary>
    public static IServiceCollection AddExecutionServices(
        this IServiceCollection services,
        bool registerDefaultScriptProvider = true)
    {
        services.AddExecutionCore(registerDefaultScriptProvider);
        services.AddInHostMcpServer();
        return services;
    }

    /// <summary>
    /// Script/assembly providers, orchestrator, packages, and Python/C#/F# runtimes.
    /// </summary>
    public static IServiceCollection AddExecutionCore(
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

        return services;
    }

    /// <summary>
    /// In-host MCP catalog, registry providers, built-in execute tools, and named-pipe MCP server.
    /// </summary>
    public static IServiceCollection AddInHostMcpServer(this IServiceCollection services)
    {
        services.AddSingleton<ConnectionState>();
        services.AddSingleton<IMcpExecutionTracker, ConnectionStateExecutionTracker>();

        services.AddSingleton<DotnetMcpRegistryProvider>();
        services.AddSingleton<PythonMcpRegistryProvider>();
        services.AddSingleton<BuiltInMcpRegistryProvider>();
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<DotnetMcpRegistryProvider>());
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<PythonMcpRegistryProvider>());
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<BuiltInMcpRegistryProvider>());
        services.AddSingleton<IMcpHostExecution, HostContextMcpExecution>();
        services.AddSingleton<IMcpServerPrimitiveAdapter, DotnetMcpServerPrimitiveAdapter>();
        services.AddSingleton<IMcpServerPrimitiveAdapter, PythonMcpServerPrimitiveAdapter>();
        services.AddSingleton<McpCatalogLoader>();
        services.AddSingleton<McpCatalogStore>();
        services.AddSingleton<IBuiltInMcpTool, CSharpCodeTool>();
        services.AddSingleton<IBuiltInMcpTool, PythonCodeTool>();
        services.AddSingleton<IBuiltInMcpTool>(sp =>
            new OpenDocumentTool(sp.GetService<IDocumentBridge>() ?? NullDocumentBridge.Instance));
        services.AddSingleton<McpServerPrimitiveCollection<McpServerTool>>(_ => []);
        services.AddSingleton<McpServerPrimitiveCollection<McpServerPrompt>>(_ => []);
        services.AddSingleton<McpServerResourceCollection>(_ => []);
        services.AddHostMcpPipeServer();

        return services;
    }
}
