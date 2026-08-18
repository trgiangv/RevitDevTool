using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Wraps a per-toolset kernel isolation session.
/// </summary>
public sealed class McpToolsetContext(string toolsetDllPath, ILogger? logger = null) : IDisposable
{
    private readonly string _toolsetPath = Path.GetFullPath(toolsetDllPath);
    private Assembly? _loadedAssembly;
    private bool _disposed;

    private AssemblyIsolationSession? _session;

    public Assembly LoadAssembly()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(McpToolsetContext));

        if (_loadedAssembly is not null)
            return _loadedAssembly;

        _session = AssemblyIsolationSession.Create(
            McpToolsetIsolationPlan.Create(_toolsetPath, new LoggerDiagnosticSink(logger)));
        _loadedAssembly = _session.LoadEntryAssembly();
        return _loadedAssembly;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _session?.Dispose();
        _session = null;
        _loadedAssembly = null;
    }

    private sealed class LoggerDiagnosticSink(ILogger? logger) : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic)
        {
            logger?.ZLogDebug($"[McpToolsetContext] [{diagnostic.Code}] {diagnostic.Message}");
        }
    }
}
