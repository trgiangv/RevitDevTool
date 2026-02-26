using System.Collections.ObjectModel;
using RevitDevTool.CodeExecute.Interfaces;
namespace RevitDevTool.CodeExecute.Models;

/// <summary>
/// Base class for all nodes in the tree structure.
/// Unified model that replaces multiple node hierarchies (AssemblyNode, NamespaceNode, CommandNode, GroupNode, ScriptNode).
/// Implements Composite Pattern for tree structure.
/// </summary>
public abstract partial class BaseNode : ObservableObject
{
    /// <summary>
    /// Unique path-based identifier that survives object recreation.
    /// Examples:
    /// - DotNet: "dotnet://C:/Plugins/Tools.dll|MyCompany.Commands.PurgeCommand"
    /// - Python: "python://View/Cleanup//HideUnused.py"
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Internal name (file name, class name, etc.) - also used as display name
    /// </summary>
    public required string Name { get; init; }


    /// <summary>
    /// Child nodes (Composite Pattern)
    /// </summary>
    public ObservableCollection<BaseNode> Children { get; } = [];

    /// <summary>
    /// Whether the node is expanded in the tree
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Whether the node is selected in the tree
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Whether the node is visible (used for search filtering)
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Highlight range for search results
    /// </summary>
    [ObservableProperty]
    private ISelectionRange? _highlightRange;

    /// <summary>
    /// Whether this node is the last executed item (for UI indicator)
    /// </summary>
    [ObservableProperty]
    private bool _isLastExecuted;

    /// <summary>
    /// Last executed time
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastExecutedTime;

    /// <summary>
    /// Execution strategy (null for container nodes)
    /// </summary>
    public IExecutionStrategy? ExecutionStrategy { get; init; }

    /// <summary>
    /// Type of node (Container or Executable)
    /// </summary>
    public required NodeType NodeType { get; init; }


    /// <summary>
    /// Whether the node can be executed
    /// </summary>
    public bool IsExecutable => NodeType == NodeType.Executable && ExecutionStrategy != null;

    /// <summary>
    /// Execute this node if executable
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!IsExecutable)
            return ExecutionResult.Skipped();

        try
        {
            progress?.Report($"Executing '{Name}'...");
            var result = await ExecutionStrategy!.ExecuteAsync(progress, cancellationToken);
            IsLastExecuted = result.Success;
            return result;
        }
        catch (OperationCanceledException)
        {
            IsLastExecuted = false;
            return ExecutionResult.Cancelled("Execution cancelled by user.");
        }
        catch (Exception ex)
        {
            IsLastExecuted = false;
            return ExecutionResult.Failed(ex.Message, ex);
        }
        finally
        {
            LastExecutedTime = DateTime.Now;
        }
    }
}