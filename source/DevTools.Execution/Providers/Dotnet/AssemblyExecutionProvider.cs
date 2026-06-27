using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
namespace DevTools.Execution.Providers.Dotnet;

/// <summary>
/// Provider for discovering and executing .NET assemblies.
/// Uses unified node model: RootNode (Assembly) -> IntermediateNode (Namespace) -> ExecuteNode (Command)
/// </summary>
public sealed class AssemblyExecutionProvider(
    ICommandDiscovery commandDiscovery,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner) : IExecutionProvider
{
    public string Name => "DotNet";

    public int Priority => 100;

    public bool CanHandle(string path)
        => File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    public Task<IEnumerable<ExecutionNodeBase>> DiscoverAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(path) || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                Trace.TraceWarning($"Invalid assembly path: {path}");
                return [];
            }
            var addinItems = commandDiscovery.ParseCommands(path);

            if (addinItems.Count == 0)
            {
                Trace.TraceWarning($"No commands found in assembly: {path}");
                return Enumerable.Empty<ExecutionNodeBase>();
            }

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

    private ExecutionNodeRoot BuildAssemblyNode(string assemblyPath, List<CommandItem> commands)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var assemblyId = $"dotnet://{assemblyPath}";

        var assemblyNode = new ExecutionNodeRoot
        {
            Id = assemblyId,
            Name = assemblyName,
            RootPath = assemblyPath,
            ContainerMode = ContainerMode.Assembly,
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

    private ExecutionNodeIntermediate BuildNamespaceNode(string namespaceName, IEnumerable<CommandItem> commands, string assemblyPath)
    {
        var namespaceId = $"dotnet://{assemblyPath}|{namespaceName}";

        var namespaceNode = new ExecutionNodeIntermediate
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

    private ExecutionNode BuildCommandNode(CommandItem commandItem)
    {
        var commandId = $"dotnet://{commandItem.AssemblyPath}|{commandItem.FullClassName}";

        return new ExecutionNode
        {
            Id = commandId,
            Name = commandItem.Name,
            ExecutablePath = commandItem.FullClassName,
            SourceFilePath = commandItem.AssemblyPath,
            ContainerMode = ContainerMode.Assembly,
            ExecutionMode = ExecutionMode.Dotnet,
            NodeType = NodeType.Executable,
            ExecutionStrategy = new AssemblyExecutionStrategy(commandItem, hostContext, commandRunner)
        };
    }

    private static string ExtractNamespace(string fullClassName)
    {
        var lastDot = fullClassName.LastIndexOf('.');
        return lastDot > 0 ? fullClassName[..lastDot] : "(Global)";
    }

    #endregion
}
