using System.Diagnostics;
using System.IO;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.FSharp;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.McpParser.Models;
namespace RevitDevTool.Execution.Providers;

public sealed class ScriptExecutionProvider : IExecutionProvider
{
    private static readonly string[] ScriptSearchPatterns = ["*.py", "*.fsx"];

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ide/git
        ".git", ".github", ".vs", ".idea", ".vscode", ".cursor",
        // Docs
        "docs", "doc", "img", "assets", "resources",
        // Agents
        ".agent", ".agents", ".claude",
        // Build
        "bin", "obj", "packages", "node_modules", "output",
        "__pycache__", "dist", "build", ".mypy_cache", ".pytest_cache",
        // Virtual envs
        "venv", ".venv", "env", ".env", "virtualenv", ".pixi", 
    };

    public string Name => "Script";

    public int Priority => -100;

    public bool CanHandle(string path) => Directory.Exists(path);

    public Task<IEnumerable<ExecutionNodeBase>> DiscoverAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(path))
            {
                Trace.TraceWarning($"Invalid directory path: {path}");
                return Enumerable.Empty<ExecutionNodeBase>();
            }

            var rootNode = BuildFolderTree(path, path);
            return rootNode is not null ? [rootNode] : [];
        }, cancellationToken);
    }

    public IEnumerable<string> GetWatchPatterns()
    {
        return ScriptSearchPatterns;
    }

    public bool ValidatePath(string path)
    {
        return Directory.Exists(path);
    }

    private ExecutionNodeBase? BuildFolderTree(string rootPath, string currentPath)
    {
        if (ShouldSkipRootFolder(rootPath, currentPath))
        {
            return null;
        }

        var folderNode = CreateFolderNode(rootPath, currentPath);

        PopulateScripts(folderNode, currentPath, rootPath);
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
        if (!currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
            return false;

        if (HasAnyEntryScript(rootPath)) return false;
        Trace.TraceWarning($"No valid entry script found (*script.py or *script.fsx) in: {rootPath}");
        return true;
    }

    private static ExecutionNodeBase CreateFolderNode(string rootPath, string currentPath)
    {
        var folderName = Path.GetFileName(currentPath);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = currentPath;
        }

        var folderId = $"script://{currentPath}";
        var isRoot = currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase);

        if (isRoot)
        {
            return new ExecutionNodeRoot
            {
                Id = folderId,
                Name = folderName,
                RootPath = currentPath,
                ProviderType = ExecutionMode.Script,
                NodeType = NodeType.Container,
                IsExpanded = true
            };
        }

        return new ExecutionNodeIntermediate
        {
            Id = folderId,
            Name = folderName,
            FullPath = currentPath,
            NodeType = NodeType.Container,
            IsExpanded = true
        };
    }

    private static void PopulateScripts(ExecutionNodeBase folder, string currentPath, string rootPath)
    {
        var scriptFiles = ScriptSearchPatterns
            .SelectMany(pattern => Directory.GetFiles(currentPath, pattern, SearchOption.TopDirectoryOnly))
            .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("script", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName);

        foreach (var scriptFile in scriptFiles)
        {
            var scriptNode = BuildScriptNode(scriptFile, rootPath);
            folder.Children.Add(scriptNode);
        }
    }

    private static ExecutionNode BuildScriptNode(string scriptPath, string rootPath)
    {
        var fileName = Path.GetFileName(scriptPath);
        var extension = Path.GetExtension(scriptPath);

        return extension.ToLowerInvariant() switch
        {
            ".py" => new ExecutionNode
            {
                Id = $"python://{scriptPath}",
                Name = fileName,
                ExecutablePath = scriptPath,
                SourceFilePath = scriptPath,
                ProviderType = ExecutionMode.Python,
                NodeType = NodeType.Executable,
                ExecutionStrategy = new PythonExecutionStrategy(scriptPath, rootPath)
            },
            ".fsx" => new ExecutionNode
            {
                Id = $"fsharp://{scriptPath}",
                Name = fileName,
                ExecutablePath = scriptPath,
                SourceFilePath = scriptPath,
                ProviderType = ExecutionMode.FSharp,
                NodeType = NodeType.Executable,
                ExecutionStrategy = new FSharpExecutionStrategy(scriptPath)
            },
            _ => throw new NotSupportedException($"Unsupported script extension '{extension}' for file '{scriptPath}'.")
        };
    }

    private void PopulateSubFolders(ExecutionNodeBase folder, string rootPath, string currentPath)
    {
        var subFolders = Directory.GetDirectories(currentPath)
            .Where(d => !IsIgnoredFolder(Path.GetFileName(d)))
            .OrderBy(Path.GetFileName);

        foreach (var subFolder in subFolders)
        {
            var subFolderNode = BuildFolderTree(rootPath, subFolder);
            if (subFolderNode != null)
            {
                folder.Children.Add(subFolderNode);
            }
        }
    }

    private static bool IsIgnoredFolder(string folderName)
    {
        return IgnoredFolders.Contains(folderName);
    }

    private static bool HasAnyEntryScript(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*script.py", SearchOption.AllDirectories).Length != 0
               || Directory.GetFiles(rootPath, "*script.fsx", SearchOption.AllDirectories).Length != 0;
    }
}
