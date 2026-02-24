using System.Diagnostics;
using System.IO;
using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.CodeExecute.Models;

namespace RevitDevTool.CodeExecute.Providers.FSharp;

/// <summary>
/// Provider for discovering and executing F# scripts (.fsx).
/// Discovers *script.fsx files in folder trees, mirroring PythonExecutionProvider.
/// </summary>
public sealed class FSharpExecutionProvider : IExecutionProvider
{
    public string Name => "FSharp";

    public int Priority => -100;

    public bool CanHandle(string path) => Directory.Exists(path);

    public Task<IEnumerable<BaseNode>> DiscoverAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(path))
            {
                Trace.TraceWarning($"Invalid directory path: {path}");
                return Enumerable.Empty<BaseNode>();
            }

            var rootNode = BuildFolderTree(path, path);
            return rootNode is not null ? [rootNode] : [];
        }, cancellationToken);
    }

    public IEnumerable<string> GetWatchPatterns()
    {
        return ["*.fsx"];
    }

    public bool ValidatePath(string path)
    {
        return Directory.Exists(path);
    }

    #region Private Helpers

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", ".idea", ".vscode",
        "bin", "obj", "packages", "node_modules", "output",
        "venv", ".venv", "env", ".env", "virtualenv",
        "__pycache__", "dist", "build", ".mypy_cache", ".pytest_cache"
    };

    private static bool IsIgnoredFolder(string folderName) => IgnoredFolders.Contains(folderName);

    private static BaseNode? BuildFolderTree(string rootPath, string currentPath)
    {
        if (ShouldSkipRootFolder(rootPath, currentPath)) return null;

        var folderNode = CreateFolderNode(rootPath, currentPath);

        PopulateScripts(folderNode, currentPath);
        PopulateSubFolders(folderNode, rootPath, currentPath);

        var isRoot = currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase);
        if (!isRoot && folderNode.Children.Count == 0)
        {
            return null;
        }

        return folderNode;
    }

    private static bool ShouldSkipRootFolder(string rootPath, string currentPath)
    {
        if (!currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) return false;

        var allScripts = Directory.GetFiles(rootPath, "*script.fsx", SearchOption.AllDirectories);
        if (allScripts.Length != 0) return false;
        Trace.TraceWarning($"No valid F# scripts (*script.fsx) found in: {rootPath}");
        return true;
    }

    private static BaseNode CreateFolderNode(string rootPath, string currentPath)
    {
        var folderName = Path.GetFileName(currentPath);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = currentPath;
        }

        var folderId = $"fsharp://{currentPath}";
        var isRoot = currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase);

        if (isRoot)
        {
            return new RootNode
            {
                Id = folderId,
                Name = folderName,
                RootPath = currentPath,
                ProviderType = ExecutionMode.FSharp,
                NodeType = NodeType.Container,
                IsExpanded = true
            };
        }

        return new IntermediateNode
        {
            Id = folderId,
            Name = folderName,
            FullPath = currentPath,
            NodeType = NodeType.Container,
            IsExpanded = true
        };
    }

    private static void PopulateScripts(BaseNode folderNode, string currentPath)
    {
        var scriptFiles = Directory.GetFiles(currentPath, "*.fsx", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("script", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName);

        foreach (var scriptFile in scriptFiles)
        {
            var scriptNode = BuildScriptNode(scriptFile);
            folderNode.Children.Add(scriptNode);
        }
    }

    private static void PopulateSubFolders(BaseNode folderNode, string rootPath, string currentPath)
    {
        var subFolders = Directory.GetDirectories(currentPath)
            .Where(d => !IsIgnoredFolder(Path.GetFileName(d)))
            .OrderBy(Path.GetFileName);

        foreach (var subFolder in subFolders)
        {
            var subFolderNode = BuildFolderTree(rootPath, subFolder);
            if (subFolderNode != null)
            {
                folderNode.Children.Add(subFolderNode);
            }
        }
    }

    private static ExecuteNode BuildScriptNode(string scriptPath)
    {
        var fileName = Path.GetFileName(scriptPath);
        var scriptId = $"fsharp://{scriptPath}";

        return new ExecuteNode
        {
            Id = scriptId,
            Name = fileName,
            ExecutablePath = scriptPath,
            SourceFilePath = scriptPath,
            ProviderType = ExecutionMode.FSharp,
            NodeType = NodeType.Executable,
            ExecutionStrategy = new FSharpExecutionStrategy(scriptPath)
        };
    }

    #endregion
}
