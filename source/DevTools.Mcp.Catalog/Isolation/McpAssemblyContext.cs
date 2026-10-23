using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Reflection;
using ZLogger;

namespace DevTools.Mcp.Isolation;

/// <summary>
/// Wraps a per-toolset kernel isolation session.
/// </summary>
public sealed class McpAssemblyContext(string assemblyPath, ILogger? logger = null) : IDisposable
{
    private readonly string _toolsetPath = Path.GetFullPath(assemblyPath);
    private Assembly? _loadedAssembly;
    private bool _disposed;

    private AssemblyIsolationSession? _session;

    public Assembly LoadAssembly()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_loadedAssembly is not null)
            return _loadedAssembly;

        _session = AssemblyIsolationSession.Create(
            McpAssemblyIsolationPlan.Create(_toolsetPath, new LoggerDiagnosticSink(logger)));
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
            logger?.ZLogDebug($"[McpAssemblyContext] [{diagnostic.Code}] {diagnostic.Message}");
        }
    }
}
