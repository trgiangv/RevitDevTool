using System.IO;
using DevTools.Execution.Providers.Python;
namespace DevTools.Execution.External.Testing;

/// <summary>
/// CPython <c>tests/run</c> only. Resolves PEP 723 metadata from test files and
/// <c>conftest.py</c> into the host pixi-env (Python.NET). IronPython
/// <c>ipytests/run</c> never uses this service — IPy 2.7/3.4 cannot import
/// those wheels.
/// </summary>
public sealed class PytestDependencyService(PythonInitializer pythonInitializer)
{
    public async Task PrepareRunAsync(PytestRunRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePythonReadyAsync().ConfigureAwait(false);

        foreach (var path in GetRunDependencyPaths(request))
            await ResolveDependenciesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsurePythonReadyAsync()
    {
        await pythonInitializer.InitializeAsync().ConfigureAwait(false);

        if (!pythonInitializer.IsInitialized || pythonInitializer.Provider is null)
            throw new InvalidOperationException("Python runtime is not initialized.");
    }

    private async Task ResolveDependenciesAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return;

        var resolved = await PythonExecutionStrategy
            .ResolveDependenciesAsync(pythonInitializer, path, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!resolved)
            throw new InvalidOperationException($"Dependency resolution failed for '{path}'.");
    }

    private static List<string> GetRunDependencyPaths(PytestRunRequest request)
    {
        var workspaceRoot = PytestPathResolver.ResolveWorkspaceRoot(request.WorkspaceRoot, request.TestRoot);
        var testRoot = PytestPathResolver.ResolvePath(request.TestRoot, workspaceRoot);
        var paths = new List<string>();

        foreach (var nodeId in request.NodeIds)
        {
            var testFilePath = TryResolveNodeFilePath(nodeId, workspaceRoot, testRoot);
            if (testFilePath is null)
                continue;

            paths.Add(testFilePath);

            var directory = Path.GetDirectoryName(testFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                paths.AddRange(EnumerateConftestChain(directory, workspaceRoot));
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> EnumerateConftestChain(string startDirectory, string workspaceRoot)
    {
        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var results = new List<string>();
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (current is not null)
        {
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

        var filePart = nodeId.Split([IpyTestPath.NodeidSeparator], 2, StringSplitOptions.None)[0]
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
