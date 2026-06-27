using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers;

/// <summary>Folder tree for <c>*script.py</c> (CPython or IronPython), <c>*script.fsx</c>, and <c>*script.csx</c>.</summary>
public sealed class ScriptExecutionProvider(
    IScriptExecutionStrategyFactory strategyFactory,
    ILogger<ScriptExecutionProvider> logger) : IExecutionProvider
{
    private static readonly string[] WatchPatterns = ["*script.py", "*script.fsx", "*script.csx"];

    private const string IronPythonEntryFileSuffix = "_ipy_script.py";

    private static readonly HashSet<string> SkippedScriptSubfolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", ".idea", ".vscode", ".cursor",
        "docs", "doc", "img", "assets", "resources",
        ".agent", ".agents", ".claude",
        "bin", "obj", "packages", "node_modules", "output",
        "__pycache__", "dist", "build", ".mypy_cache", ".pytest_cache",
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
                logger.ZLogWarning($"Invalid directory path: {path}");
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
            return null;

        var folderNode = CreateFolderNode(rootPath, currentPath);
        PopulateScripts(folderNode, currentPath, rootPath);
        PopulateSubFolders(folderNode, rootPath, currentPath);

        var isRoot = currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase);
        if (!isRoot && folderNode.Children.Count == 0)
            return null;

        return folderNode;
    }

    private bool ShouldSkipRootFolder(string rootPath, string currentPath)
    {
        if (!currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
            return false;

        if (HasAnyEntryScript(rootPath))
            return false;

        logger.ZLogWarning($"No valid entry script found (*script.py, *script.fsx, or *script.csx) in: {rootPath}");
        return true;
    }

    private static ExecutionNodeBase CreateFolderNode(string rootPath, string currentPath)
    {
        var folderName = Path.GetFileName(currentPath);
        if (string.IsNullOrEmpty(folderName))
            folderName = currentPath;

        var folderId = $"script://{currentPath}";
        var isRoot = currentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase);

        if (isRoot)
        {
            return new ExecutionNodeRoot
            {
                Id = folderId,
                Name = folderName,
                RootPath = currentPath,
                ContainerMode = ContainerMode.Script,
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
        var scriptFiles = WatchPatterns
            .SelectMany(pattern => Directory.GetFiles(currentPath, pattern, SearchOption.TopDirectoryOnly))
            .OrderBy(Path.GetFileName);

        foreach (var scriptFile in scriptFiles)
            folder.Children.Add(BuildScriptNode(scriptFile, rootPath));
    }

    private ExecutionNode BuildScriptNode(string scriptPath, string rootPath)
    {
        var fileName = Path.GetFileName(scriptPath);
        var mode = GetExecutionMode(scriptPath);

        return new ExecutionNode
        {
            Id = $"{GetScheme(mode)}://{scriptPath}",
            Name = fileName,
            ExecutablePath = scriptPath,
            SourceFilePath = scriptPath,
            ContainerMode = ContainerMode.Script,
            ExecutionMode = mode,
            NodeType = NodeType.Executable,
            ExecutionStrategy = strategyFactory.Create(mode, scriptPath, rootPath)
        };
    }

    private static ExecutionMode GetExecutionMode(string scriptPath)
    {
        var extension = Path.GetExtension(scriptPath);

        return extension.ToLowerInvariant() switch
        {
            ".py" when IsIronPythonEntryScript(scriptPath) => ExecutionMode.IronPython,
            ".py" => ExecutionMode.Python,
            ".fsx" => ExecutionMode.FSharp,
            ".csx" => ExecutionMode.CSharp,
            _ => throw new NotSupportedException(
                $"Unsupported script extension '{extension}' for file '{scriptPath}'.")
        };
    }

    private static string GetScheme(ExecutionMode mode) =>
        mode switch
        {
            ExecutionMode.Python => "python",
            ExecutionMode.IronPython => "ironpython",
            ExecutionMode.FSharp => "fsharp",
            ExecutionMode.CSharp => "csharp",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    private void PopulateSubFolders(ExecutionNodeBase folder, string rootPath, string currentPath)
    {
        var subFolders = Directory.GetDirectories(currentPath)
            .Where(d => !IsSkippedScriptSubfolder(Path.GetFileName(d)))
            .OrderBy(Path.GetFileName);

        foreach (var subFolder in subFolders)
        {
            var subFolderNode = BuildFolderTree(rootPath, subFolder);
            if (subFolderNode is not null)
                folder.Children.Add(subFolderNode);
        }
    }

    private static bool IsSkippedScriptSubfolder(string folderName) =>
        !string.IsNullOrWhiteSpace(folderName) && SkippedScriptSubfolderNames.Contains(folderName);

    private static bool IsIronPythonEntryScript(string filePath) =>
        filePath.EndsWith(IronPythonEntryFileSuffix, StringComparison.OrdinalIgnoreCase);

    private static bool HasAnyEntryScript(string rootPath) =>
        WatchPatterns.Any(pattern =>
            Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories).Length != 0);
}
