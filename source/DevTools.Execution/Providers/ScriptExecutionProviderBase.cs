using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;
using DevTools.McpParser.Models;

namespace DevTools.Execution.Providers;

/// <summary>Folder tree for <c>*script.py</c> (CPython or IronPython) and <c>*script.fsx</c>; engine is chosen per file in <c>BuildScriptNode</c>.</summary>
public sealed class ScriptExecutionProvider(
    PythonInitializer pythonInitializer,
    PythonExecutor executor,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner) : IExecutionProvider
{
    private static readonly string[] WatchPatterns = ["*script.py", "*script.fsx"];
    
    private const string IronPythonEntryFileSuffix = "_ipy_script.py";

    private static readonly HashSet<string> SkippedScriptSubfolderNames = new(StringComparer.OrdinalIgnoreCase)
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

    public IEnumerable<string> GetWatchPatterns() => WatchPatterns;

    public bool ValidatePath(string path) => Directory.Exists(path);

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

    private void PopulateScripts(ExecutionNodeBase folder, string currentPath, string rootPath)
    {
        var scriptFiles = Directory.GetFiles(currentPath, "*script.py", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(currentPath, "*script.fsx", SearchOption.TopDirectoryOnly))
            .OrderBy(Path.GetFileName);

        foreach (var scriptFile in scriptFiles)
        {
            var scriptNode = BuildScriptNode(scriptFile, rootPath);
            folder.Children.Add(scriptNode);
        }
    }

    private ExecutionNode BuildScriptNode(string scriptPath, string rootPath)
    {
        var fileName = Path.GetFileName(scriptPath);
        var extension = Path.GetExtension(scriptPath);

        return extension.ToLowerInvariant() switch
        {
            ".py" when IsIronPythonEntryScript(scriptPath) => new ExecutionNode
            {
                Id = $"ironpython://{scriptPath}",
                Name = fileName,
                ExecutablePath = scriptPath,
                SourceFilePath = scriptPath,
                ProviderType = ExecutionMode.IronPython,
                NodeType = NodeType.Executable,
                ExecutionStrategy = new IronPythonExecutionStrategy(scriptPath, rootPath, ironPythonBridge, hostContext)
            },
            ".py" => new ExecutionNode
            {
                Id = $"python://{scriptPath}",
                Name = fileName,
                ExecutablePath = scriptPath,
                SourceFilePath = scriptPath,
                ProviderType = ExecutionMode.Python,
                NodeType = NodeType.Executable,
                ExecutionStrategy = new PythonExecutionStrategy(scriptPath, rootPath, pythonInitializer, executor, hostContext)
            },
            ".fsx" => new ExecutionNode
            {
                Id = $"fsharp://{scriptPath}",
                Name = fileName,
                ExecutablePath = scriptPath,
                SourceFilePath = scriptPath,
                ProviderType = ExecutionMode.FSharp,
                NodeType = NodeType.Executable,
                ExecutionStrategy = new FSharpExecutionStrategy(scriptPath, hostContext, commandRunner)
            },
            _ => throw new NotSupportedException($"Unsupported script extension '{extension}' for file '{scriptPath}'.")
        };
    }

    private void PopulateSubFolders(ExecutionNodeBase folder, string rootPath, string currentPath)
    {
        var subFolders = Directory.GetDirectories(currentPath)
            .Where(d => !IsSkippedScriptSubfolder(Path.GetFileName(d)))
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

    private static bool IsSkippedScriptSubfolder(string folderName)
    {
        return !string.IsNullOrWhiteSpace(folderName) && SkippedScriptSubfolderNames.Contains(folderName);
    }

    private static bool IsIronPythonEntryScript(string filePath)
    {
        return filePath.EndsWith(IronPythonEntryFileSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyEntryScript(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*script.py", SearchOption.AllDirectories).Length != 0
               || Directory.GetFiles(rootPath, "*script.fsx", SearchOption.AllDirectories).Length != 0;
    }
}
