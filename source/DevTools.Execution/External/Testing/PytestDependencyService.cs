using System.IO;
using DevTools.Execution.Providers.Python;
namespace DevTools.Execution.External.Testing;

public class PytestDependencyService(PythonInitializer pythonInitializer)
{
    public virtual async Task PrepareRunAsync(PytestRunRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsurePythonReadyAsync(cancellationToken).ConfigureAwait(false);

        foreach (var path in GetRunDependencyPaths(request, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ResolveDependenciesAsync(path, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsurePythonReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await pythonInitializer.InitializeAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!pythonInitializer.IsInitialized || pythonInitializer.Provider is null)
            throw new InvalidOperationException("Python runtime is not initialized.");
    }

    private async Task ResolveDependenciesAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path))
            return;

        var resolved = await PythonExecutionStrategy
            .ResolveDependenciesAsync(pythonInitializer, path, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!resolved)
            throw new InvalidOperationException($"Dependency resolution failed for '{path}'.");
    }

    private static List<string> GetRunDependencyPaths(PytestRunRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workspaceRoot = PytestPathResolver.ResolveWorkspaceRoot(request.WorkspaceRoot, request.TestRoot);
        var testRoot = PytestPathResolver.ResolvePath(request.TestRoot, workspaceRoot);
        var paths = new List<string>();

        foreach (var nodeId in request.NodeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var testFilePath = TryResolveNodeFilePath(nodeId, workspaceRoot, testRoot);
            if (testFilePath is null)
                continue;

            paths.Add(testFilePath);

            var directory = Path.GetDirectoryName(testFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                paths.AddRange(EnumerateConftestChain(directory, workspaceRoot, cancellationToken));
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> EnumerateConftestChain(
        string startDirectory,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var results = new List<string>();
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (current is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var conftestPath = Path.Combine(current.FullName, "conftest.py");
            if (File.Exists(conftestPath))
                results.Add(conftestPath);

            if (string.Equals(
                    current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    normalizedWorkspaceRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = current.Parent;
        }

        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? TryResolveNodeFilePath(string nodeId, string workspaceRoot, string testRoot)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        var filePart = nodeId.Split(["::"], 2, StringSplitOptions.None)[0]
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(filePart))
            return Path.GetFullPath(filePart);

        var testRootBase = Directory.Exists(testRoot)
            ? testRoot
            : Path.GetDirectoryName(testRoot) ?? workspaceRoot;

        var testRootCandidate = Path.GetFullPath(Path.Combine(testRootBase, filePart));
        return File.Exists(testRootCandidate) ? testRootCandidate : Path.GetFullPath(Path.Combine(workspaceRoot, filePart));
    }
}
