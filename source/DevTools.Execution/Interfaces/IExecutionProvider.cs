using DevTools.Execution.Models;
namespace DevTools.Execution.Interfaces;

/// <summary>
/// Provider interface for discovering and executing code.
/// Implements Provider Pattern for extensibility.
/// Each provider handles one execution mode (DotNet, Python, Dynamo, etc.)
/// </summary>
public interface IExecutionProvider
{
    /// <summary>
    /// Internal name of the provider (e.g., "DotNet", "Python", "Dynamo")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Priority for provider resolution when multiple providers can handle the same path.
    /// Higher priority providers are checked first. Default should be 0.
    /// Use higher values (e.g., 100) for specific providers like DotNet (.dll files)
    /// Use lower values (e.g., -100) for fallback providers like Python (folders)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Check if this provider can handle the given path.
    /// Used by Orchestrator to auto-detect the correct provider.
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns>True if this provider can handle the path</returns>
    bool CanHandle(string path);

    /// <summary>
    /// Discover nodes from the given path or resource
    /// </summary>
    /// <param name="path">Path to discover (file, folder, assembly, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Discovered nodes</returns>
    Task<IEnumerable<ExecutionNodeBase>> DiscoverAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get file watch patterns for auto-reload
    /// </summary>
    /// <returns>File patterns to watch (e.g., "*.dll", "*.py", "*.dyn")</returns>
    IEnumerable<string> GetWatchPatterns();

    /// <summary>
    /// Validate if the path is valid for this provider (more thorough than CanHandle)
    /// </summary>
    /// <param name="path">Path to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidatePath(string path);
}