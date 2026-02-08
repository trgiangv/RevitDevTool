using RevitDevTool.CodeExecute.Providers.DotNet.Models;
using System.Diagnostics;
using System.IO;
using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.CodeExecute.Models;

namespace RevitDevTool.CodeExecute.Providers.DotNet;

/// <summary>
/// Provider for discovering and executing .NET assemblies.
/// Uses unified node model: RootNode (Assembly) → IntermediateNode (Namespace) → ExecuteNode (Command)
/// </summary>
public sealed class DotNetExecutionProvider : IExecutionProvider
{
    public string Name => "DotNet";

    /// <summary>
    /// High priority - DLL files are more specific than folders
    /// </summary>
    public int Priority => 100;

    /// <summary>
    /// Check if this is a .dll file
    /// </summary>
    public bool CanHandle(string path)
        => File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    public Task<IEnumerable<BaseNode>> DiscoverAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(path) || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                Trace.TraceWarning($"Invalid assembly path: {path}");
                return [];
            }

            // Use existing logic to parse commands
            var addinItems = AddinLoaderService.ParseCommands(path);

            if (addinItems.Count == 0)
            {
                Trace.TraceWarning($"No commands found in assembly: {path}");
                return Enumerable.Empty<BaseNode>();
            }

            // Build tree: Assembly -> Namespace -> Command
            var assemblyNode = BuildAssemblyNode(path, addinItems);
            return [assemblyNode];

        }, cancellationToken);
    }

    public IEnumerable<string> GetWatchPatterns()
    {
        return ["*.dll"];
    }

    public bool ValidatePath(string path)
    {
        return File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
    }

    #region Private Helpers

    private static RootNode BuildAssemblyNode(string assemblyPath, List<AddinItem> commands)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var assemblyId = $"dotnet://{assemblyPath}";

        var assemblyNode = new RootNode
        {
            Id = assemblyId,
            Name = assemblyName,
            RootPath = assemblyPath,
            ProviderType = ExecutionMode.DotNet,
            NodeType = NodeType.Container
        };

        var namespaceGroups = commands.GroupBy(cmd => ExtractNamespace(cmd.FullClassName)).OrderBy(g => g.Key);

        foreach (var nsGroup in namespaceGroups)
        {
            var namespaceNode = BuildNamespaceNode(nsGroup.Key, nsGroup, assemblyPath);
            assemblyNode.Children.Add(namespaceNode);
        }

        return assemblyNode;
    }

    private static IntermediateNode BuildNamespaceNode(string namespaceName, IEnumerable<AddinItem> commands, string assemblyPath)
    {
        var namespaceId = $"dotnet://{assemblyPath}|{namespaceName}";

        var namespaceNode = new IntermediateNode
        {
            Id = namespaceId,
            Name = namespaceName,
            FullPath = namespaceName,
            NodeType = NodeType.Container
        };

        foreach (var command in commands.OrderBy(c => c.Name))
        {
            var commandNode = BuildCommandNode(command);
            namespaceNode.Children.Add(commandNode);
        }

        return namespaceNode;
    }

    private static ExecuteNode BuildCommandNode(AddinItem addinItem)
    {
        var commandId = $"dotnet://{addinItem.AssemblyPath}|{addinItem.FullClassName}";

        return new ExecuteNode
        {
            Id = commandId,
            Name = addinItem.Name,
            ExecutablePath = addinItem.FullClassName,
            SourceFilePath = addinItem.AssemblyPath,
            ProviderType = ExecutionMode.DotNet,
            NodeType = NodeType.Executable,
            ExecutionStrategy = new DotNetExecutionStrategy(addinItem)
        };
    }

    private static string ExtractNamespace(string fullClassName)
    {
        var lastDot = fullClassName.LastIndexOf('.');
        return lastDot > 0 ? fullClassName[..lastDot] : "(Global)";
    }

    #endregion
}