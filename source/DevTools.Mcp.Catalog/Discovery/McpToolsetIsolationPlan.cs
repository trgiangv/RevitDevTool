using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Defines MCP's contract bindings and private dependency sources for an external toolset.
/// </summary>
public static class McpToolsetIsolationPlan
{
    public static AssemblyIsolationPlan Create(
        string entryPath,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null)
    {
        var normalizedEntryPath = Path.GetFullPath(entryPath);
        var siblingDirectory = Path.GetDirectoryName(normalizedEntryPath)
            ?? throw new ArgumentException("The toolset entry path must have a directory.", nameof(entryPath));

        var plan = AssemblyIsolationPlan.Create(normalizedEntryPath)
            .WithKind(AssemblyIsolationKind.Isolated)
            .AddManagedSource(new DirectoryAssemblySource(siblingDirectory));

#if NET
        plan = plan
            .AddManagedSource(new ResolverAssemblySource(normalizedEntryPath))
            .AddNativeSource(new ResolverNativeAssemblySource(normalizedEntryPath));
#endif

        foreach (var contractAssembly in ContractAssemblies)
            plan = plan.Pin(contractAssembly);

        return diagnosticSink is null ? plan : plan.WithDiagnosticSink(diagnosticSink);
    }

    private static IEnumerable<Assembly> ContractAssemblies => new[]
    {
        typeof(McpServer).Assembly,
        typeof(McpServerToolAttribute).Assembly,
        typeof(McpServerResourceAttribute).Assembly,
        typeof(CallToolResult).Assembly,
        typeof(Resource).Assembly,
        typeof(ResourceTemplate).Assembly,
    }
    .Distinct();
}
