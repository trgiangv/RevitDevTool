using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Testing;
using DevTools.Mcp.BuiltIn;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Execution.Pytest;

public static class PytestHostExtensions
{
    /// <summary>
    /// Host-side pytest dependency prep, runner, and <c>pytest_run</c> MCP tool.
    /// </summary>
    public static IServiceCollection AddPytestHostRunner(this IServiceCollection services)
    {
        services.AddSingleton<PytestDependencyService>();
        services.AddSingleton<PytestExecutionService>();
        services.AddSingleton<IBuiltInMcpTool, PytestRunTool>();
        return services;
    }
}
