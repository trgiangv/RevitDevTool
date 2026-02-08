using System.Diagnostics;
using System.IO;
using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.CodeExecute.Models;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Provider for discovering and executing Python scripts.
/// Uses unified node model: RootNode (RootFolder) → IntermediateNode (SubFolder) → ExecuteNode (Script ending with "Script")
/// </summary>
public sealed class PythonExecutionProvider : IExecutionProvider
{
    public string Name => "Python";

    /// <summary>
    /// Low priority - folders are generic, should be checked after specific file types
    /// </summary>
    public int Priority => -100;

    /// <summary>
    /// Check if this is a directory (folder-based provider)
    /// </summary>
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
        return ["*.py"];
    }

    public bool ValidatePath(string path)
    {
        return Directory.Exists(path);
    }

    #region Private Helpers

    private static BaseNode? BuildFolderTree(string rootPath, string currentPath)
    {
        var folderName = Path.GetFileName(currentPath);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = currentPath;
        }

        var folderId = $"python://{currentPath}";
        var isRoot = currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase);

        BaseNode folderNode;
        if (isRoot)
        {
            folderNode = new RootNode
            {
                Id = folderId,
                Name = folderName,
                RootPath = currentPath,
                ProviderType = ExecutionMode.Python,
                NodeType = NodeType.Container,
                IsExpanded = true
            };
        }
        else
        {
            folderNode = new IntermediateNode
            {
                Id = folderId,
                Name = folderName,
                FullPath = currentPath,
                NodeType = NodeType.Container,
                IsExpanded = true
            };
        }

        var scriptFiles = Directory.GetFiles(currentPath, "*.py", SearchOption.TopDirectoryOnly).Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("Script", StringComparison.OrdinalIgnoreCase)).OrderBy(Path.GetFileName);

        foreach (var scriptFile in scriptFiles)
        {
            var scriptNode = BuildScriptNode(scriptFile, rootPath);
            folderNode.Children.Add(scriptNode);
        }

        // Recursively add subfolders
        var subFolders = Directory.GetDirectories(currentPath).OrderBy(Path.GetFileName);

        foreach (var subFolder in subFolders)
        {
            var subFolderNode = BuildFolderTree(rootPath, subFolder);
            if (subFolderNode != null)
            {
                folderNode.Children.Add(subFolderNode);
            }
        }

        if (!isRoot && folderNode.Children.Count == 0)
        {
            return null;
        }

        return folderNode;
    }

    private static ExecuteNode BuildScriptNode(string scriptPath, string rootPath)
    {
        var fileName = Path.GetFileName(scriptPath);
        var scriptId = $"python://{scriptPath}";

        return new ExecuteNode
        {
            Id = scriptId,
            Name = fileName,
            ExecutablePath = scriptPath,
            SourceFilePath = scriptPath,
            ProviderType = ExecutionMode.Python,
            NodeType = NodeType.Executable,
            ExecutionStrategy = new PythonExecutionStrategy(scriptPath, rootPath)
        };
    }

    #endregion
}