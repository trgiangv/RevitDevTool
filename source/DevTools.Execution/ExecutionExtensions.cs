using DevTools.McpParser;
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
using Microsoft.Extensions.DependencyInjection;
namespace DevTools.Execution;

/// <summary>
/// Registers shared execution services.
/// Host-specific implementations (IHostContextExecutor, ICommandDiscovery, ICommandRunner,
/// IHostAppInfo, IHostPythonBridge, IFSharpHostSupport) must be registered by the host
/// before calling this method.
/// </summary>
public static class ExecutionExtensions
{
    public static void AddDevToolsExecution(this IServiceCollection services)
    {
        // Python Environment
        services.AddKeyedSingleton<PyEnvironmentProvider, PixiEnvironmentProvider>(PythonBackend.Pixi);
        services.AddKeyedSingleton<PyEnvironmentProvider, PipEnvironmentProvider>(PythonBackend.Pip);
        services.AddSingleton<PythonInitializer>();
        services.AddSingleton<PythonExecutor>();

        // Execution Services
        services.AddSingleton<ITreeStateManager, TreeStateManager>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IExecutionOrchestrator, ExecutionOrchestrator>();
        services.AddSingleton<IPackageService, PackageService>();

        // Execution Providers
        services.AddSingleton<IExecutionProvider, AssemblyExecutionProvider>();
        services.AddSingleton<IExecutionProvider, ScriptExecutionProvider>();
        services.AddKeyedSingleton<IExecutionProvider, AssemblyExecutionProvider>(ExecutionMode.Assembly);
        services.AddKeyedSingleton<IExecutionProvider, ScriptExecutionProvider>(ExecutionMode.Script);

        // IPC Bridge - Handlers
        services.AddSingleton<ConnectionState>();
        services.AddSingleton<InstanceRequestHandler>();
        services.AddSingleton<RegistryRequestHandler>();
        services.AddSingleton<PytestDependencyService>();
        services.AddSingleton<PytestExecutionService>();
        services.AddSingleton<PytestRequestHandler>();
        // MCP Registry
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
    }
}
