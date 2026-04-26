using System.Collections.ObjectModel;
using DevTools.UI.Controls;
using RevitDevTool.Execution.Interfaces;
namespace RevitDevTool.Execution.Models;

/// <summary>
/// Base class for all nodes in the tree structure.
/// </summary>
public abstract partial class TreeNodeBase : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private HighlightRange? _highlightRange;

    public abstract IEnumerable<TreeNodeBase> ChildNodes { get; }
}


/// <summary>
/// Unified model that replaces multiple node hierarchies (ExecutionRootNode, ExecutionIntermediateNode, ExecutionNode)
/// </summary>
public abstract partial class ExecutionNodeBase : TreeNodeBase
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
    /// <summary>
    /// Child nodes (Composite Pattern)
    /// </summary>
    public ObservableCollection<ExecutionNodeBase> Children { get; } = [];
    public override IEnumerable<TreeNodeBase> ChildNodes => Children;

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